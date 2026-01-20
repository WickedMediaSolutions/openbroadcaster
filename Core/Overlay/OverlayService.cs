using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Requests;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Overlay
{
    public sealed class OverlayService : IDisposable
    {
        private readonly QueueService _queueService;
        private readonly IDisposable _deckSubscription;
        private readonly OverlaySnapshotFactory _factory = new();
        private readonly OverlayDataServer _server;
        private readonly ILogger<OverlayService> _logger;
        private readonly object _sync = new();
        private readonly Dictionary<DeckIdentifier, OverlayDeckState> _deckStates = new();
        private IReadOnlyList<QueueItem> _queueItems = Array.Empty<QueueItem>();
        private IReadOnlyList<QueueItem> _historyItems = Array.Empty<QueueItem>();
        private OverlayStateSnapshot _currentSnapshot = OverlayStateSnapshot.Empty;
        private LibraryService? _libraryService;
        private RequestSettings? _requestSettings;
        private RequestPolicyEvaluator? _policyEvaluator;
        private bool _disposed;

        public OverlayService(QueueService queueService, IEventBus eventBus, ILogger<OverlayService>? logger = null)
        {
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            _logger = logger ?? AppLogger.CreateLogger<OverlayService>();
            _server = new OverlayDataServer(GetSnapshot, () => _factory.CurrentTrackFilePath);
            _deckSubscription = eventBus.Subscribe<DeckStateChangedEvent>(OnDeckStateChanged);
            _queueService.QueueChanged += OnQueueChanged;
            _queueService.HistoryChanged += OnHistoryChanged;
            SyncSnapshots();
        }

        /// <summary>
        /// Configures library access for web-based search and browsing.
        /// </summary>
        public void SetLibraryService(LibraryService? libraryService)
        {
            _libraryService = libraryService;

            if (libraryService != null)
            {
                _server.SetLibrarySearchHandler(libraryService.SearchTracks);
                _server.SetCategoriesHandler(libraryService.GetCategories);
                _server.SetCategoryTracksHandler(libraryService.GetTracksByCategory);
            }
            else
            {
                _server.SetLibrarySearchHandler(null);
                _server.SetCategoriesHandler(null);
                _server.SetCategoryTracksHandler(null);
            }
        }

        /// <summary>
        /// Configures request handling settings for web-based song requests.
        /// </summary>
        public void SetRequestSettings(RequestSettings? settings, RequestPolicyEvaluator? evaluator = null)
        {
            _requestSettings = settings;
            _policyEvaluator = evaluator ?? new RequestPolicyEvaluator();

            if (settings != null && _libraryService != null)
            {
                _server.SetRequestHandler(HandleWebRequest);
            }
            else
            {
                _server.SetRequestHandler(null);
            }
        }

        private WebRequestResult HandleWebRequest(Guid trackId, string requesterName, string? message)
        {
            if (_libraryService == null)
            {
                return new WebRequestResult { Success = false, Message = "Library service not available" };
            }

            var track = _libraryService.GetTrack(trackId);
            if (track == null)
            {
                return new WebRequestResult { Success = false, Message = "Track not found" };
            }

            if (!track.IsEnabled)
            {
                return new WebRequestResult { Success = false, Message = "Track is not available for requests" };
            }

            // Check request policy if settings are configured
            if (_requestSettings != null && _policyEvaluator != null)
            {
                var snapshot = _queueService.Snapshot();
                if (!_policyEvaluator.TryValidate(_requestSettings, snapshot, requesterName, out var reason))
                {
                    return new WebRequestResult { Success = false, Message = reason };
                }
            }

            // Create and enqueue the request
            var queueItem = new QueueItem(
                track,
                QueueSource.WebRequest,
                "Web Request",
                requesterName,
                rotationName: null,
                categoryName: null,
                requestMessage: message);

            _queueService.Enqueue(queueItem);

            var position = _queueService.Snapshot().Count;
            _logger.LogInformation("Web request queued: {Title} by {Artist}, requested by {Requester}, position {Position}",
                track.Title, track.Artist, requesterName, position);

            return new WebRequestResult
            {
                Success = true,
                Message = $"'{track.Title}' by {track.Artist} has been added to the queue",
                QueuePosition = position
            };
        }

        public void UpdateSettings(OverlaySettings? settings)
        {
            _factory.UpdateSettings(settings);
            _server.ApplySettings(settings);
            PublishSnapshot();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queueService.QueueChanged -= OnQueueChanged;
            _queueService.HistoryChanged -= OnHistoryChanged;
            _deckSubscription.Dispose();
            _server.Dispose();
        }

        private void SyncSnapshots()
        {
            lock (_sync)
            {
                _queueItems = _queueService.Snapshot();
                _historyItems = _queueService.HistorySnapshot();
            }

            PublishSnapshot();
        }

        private void OnQueueChanged(object? sender, EventArgs e)
        {
            lock (_sync)
            {
                _queueItems = _queueService.Snapshot();
            }

            PublishSnapshot();
        }

        private void OnHistoryChanged(object? sender, EventArgs e)
        {
            lock (_sync)
            {
                _historyItems = _queueService.HistorySnapshot();
            }

            PublishSnapshot();
        }

        private void OnDeckStateChanged(DeckStateChangedEvent payload)
        {
            if (payload == null)
            {
                return;
            }

            lock (_sync)
            {
                _deckStates[payload.DeckId] = new OverlayDeckState(
                    payload.DeckId,
                    payload.QueueItem,
                    payload.IsPlaying,
                    payload.Elapsed,
                    payload.Remaining);
            }

            PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            OverlayStateSnapshot snapshot;
            lock (_sync)
            {
                snapshot = _factory.Create(_deckStates.Values, _queueItems, _historyItems);
                _currentSnapshot = snapshot;
            }

            _server.Publish(snapshot);
        }

        private OverlayStateSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return _currentSnapshot;
            }
        }
    }
}
