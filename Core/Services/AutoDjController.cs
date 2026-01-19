using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class AutoDjController : IDisposable
    {
        private readonly QueueService _queueService;
        private readonly RotationEngine _rotationEngine;
        private readonly ClockwheelScheduler _clockwheel;
        private readonly LibraryService _libraryService;
        private readonly ILogger<AutoDjController> _logger;
        private readonly object _fillLock = new();
        private bool _isEnabled;
        private bool _isFilling;
        private string _statusMessage = "AutoDJ offline.";

        public AutoDjController(
            QueueService queueService,
            RotationEngine rotationEngine,
            ClockwheelScheduler clockwheel,
            LibraryService libraryService,
            ILogger<AutoDjController>? logger = null)
        {
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _rotationEngine = rotationEngine ?? throw new ArgumentNullException(nameof(rotationEngine));
            _clockwheel = clockwheel ?? throw new ArgumentNullException(nameof(clockwheel));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _logger = logger ?? AppLogger.CreateLogger<AutoDjController>();

            TargetQueueDepth = 5;
            _queueService.QueueChanged += OnQueueChanged;
            UpdateStatus(_statusMessage);
        }

        public int TargetQueueDepth { get; set; }

        public event EventHandler<string>? StatusChanged;

        public void Enable()
        {
            if (_isEnabled)
            {
                return;
            }

            _isEnabled = true;
            UpdateStatus("AutoDJ enabled. Filling queue...");
            EnsureQueueDepth();
        }

        public void Disable()
        {
            if (!_isEnabled)
            {
                return;
            }

            _isEnabled = false;
            UpdateStatus("AutoDJ paused.");
        }

        public void EnsureQueueDepth()
        {
            if (!_isEnabled)
            {
                return;
            }

            lock (_fillLock)
            {
                if (_isFilling)
                {
                    return;
                }

                _isFilling = true;
            }

            try
            {
                var attempts = 0;
                var maxAttempts = TargetQueueDepth * 2;
                while (_isEnabled && NeedsMoreTracks() && attempts < maxAttempts)
                {
                    attempts++;
                    if (!TryEnqueueNext())
                    {
                        break;
                    }
                }
            }
            finally
            {
                lock (_fillLock)
                {
                    _isFilling = false;
                }
            }
        }

        public IReadOnlyList<AutoDjPreviewItem> GetUpcomingPreview(int count)
        {
            var slots = _clockwheel.GetUpcoming(count);
            var preview = new List<AutoDjPreviewItem>(slots.Count);
            foreach (var slot in slots)
            {
                var track = ResolvePreviewTrack(slot);
                preview.Add(new AutoDjPreviewItem(
                    slot.DisplayLabel,
                    track?.Title ?? "Pending Rotation",
                    track?.Artist ?? slot.CategoryName));
            }

            return preview;
        }

        public void Dispose()
        {
            _queueService.QueueChanged -= OnQueueChanged;
        }

        private void OnQueueChanged(object? sender, EventArgs e)
        {
            if (!_isEnabled)
            {
                return;
            }

            EnsureQueueDepth();
        }

        private bool NeedsMoreTracks()
        {
            var snapshot = _queueService.Snapshot();
            return snapshot.Count < TargetQueueDepth;
        }

        private bool TryEnqueueNext()
        {
            var slot = _clockwheel.NextSlot();
            if (slot == null)
            {
                UpdateStatus("Clockwheel is empty. Configure slots to enable AutoDJ.");
                return false;
            }

            var track = ResolvePlaybackTrack(slot);
            if (track == null)
            {
                UpdateStatus($"No rotation track available for {slot.DisplayLabel}.");
                return false;
            }

            var label = string.IsNullOrWhiteSpace(slot.DisplayLabel) ? slot.CategoryName : slot.DisplayLabel;
            var queueItem = new QueueItem(track, QueueSource.AutoDj, label, string.Empty);  // No requester for AutoDJ tracks
            _queueService.Enqueue(queueItem);
            _logger.LogInformation("AutoDJ enqueued {Title} ({Label}).", track.Title, label);
            UpdateStatus($"Scheduled {track.Title} ({label}).");
            return true;
        }

        private Track? ResolvePlaybackTrack(ClockwheelSlot slot)
        {
            if (slot.TrackId.HasValue)
            {
                var direct = _libraryService.GetTrack(slot.TrackId.Value);
                if (direct != null && direct.IsEnabled)
                {
                    return direct;
                }
            }

            if (!string.IsNullOrWhiteSpace(slot.CategoryName))
            {
                var next = _rotationEngine.NextTrack(slot.CategoryName);
                if (next != null)
                {
                    return next;
                }
            }

            return null;
        }

        private Track? ResolvePreviewTrack(ClockwheelSlot slot)
        {
            if (slot.TrackId.HasValue)
            {
                var direct = _libraryService.GetTrack(slot.TrackId.Value);
                if (direct != null && direct.IsEnabled)
                {
                    return direct;
                }
            }

            if (!string.IsNullOrWhiteSpace(slot.CategoryName))
            {
                return _rotationEngine.PeekNextTrack(slot.CategoryName);
            }

            return null;
        }

        private void UpdateStatus(string message)
        {
            _statusMessage = message;
            StatusChanged?.Invoke(this, message);
        }
    }

    public sealed record AutoDjPreviewItem(string CategoryName, string TrackTitle, string TrackArtist);
}
