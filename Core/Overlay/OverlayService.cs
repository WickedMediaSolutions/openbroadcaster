using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Models;
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
