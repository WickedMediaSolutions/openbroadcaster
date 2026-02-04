using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Automation
{
    /// <summary>
    /// A simple AutoDJ service that automatically queues tracks based on a schedule.
    /// </summary>
    public class SimpleAutoDJService : IDisposable
    {
        private readonly SimpleScheduler _scheduler;
        private readonly SimpleRotationEngine _rotationEngine;
        private readonly IQueueService _queueService;
        private readonly IPlayerStatusService _playerStatusService;
        private readonly ILogger<SimpleAutoDJService> _logger;
        private System.Threading.Timer? _timer;
        private bool _isEnabled;
        private int? _lastQueuedMediaId;

        private const double EndOfTrackThresholdSeconds = 5.0; // Crossfade trigger at 5 seconds
        private const int CheckIntervalMs = 2000; // Check every 2 seconds

        /// <summary>
        /// Gets or sets a value indicating whether the AutoDJ service is active.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (_isEnabled)
                {
                    _timer?.Change(0, CheckIntervalMs);
                    _logger.LogInformation("AutoDJ service has been enabled");
                }
                else
                {
                    _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _logger.LogInformation("AutoDJ service has been disabled");
                }
            }
        }

        /// <summary>
        /// Gets the name of the currently active rotation.
        /// </summary>
        public string ActiveRotationName { get; private set; } = "None";

        public SimpleAutoDJService(
            SimpleScheduler scheduler,
            SimpleRotationEngine rotationEngine,
            IQueueService queueService,
            IPlayerStatusService playerStatusService)
        {
            _scheduler = scheduler;
            _rotationEngine = rotationEngine;
            _queueService = queueService;
            _playerStatusService = playerStatusService;
            _logger = AppLogger.CreateLogger<SimpleAutoDJService>();

            _timer = new System.Threading.Timer(OnTimerTick, state: null, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTimerTick(object? state)
        {
            // Prevent re-entrancy if the check takes longer than the interval.
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);

            try
            {
                CheckAndQueueNextTrack();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred in the AutoDJ service tick");
            }
            finally
            {
                if (IsEnabled)
                {
                    _timer?.Change(CheckIntervalMs, CheckIntervalMs);
                }
            }
        }

        // Event to notify when crossfade should be triggered
        public event Action? CrossfadeRequested;

        private void CheckAndQueueNextTrack()
        {
            var playerState = _playerStatusService.GetPlayerState();
            bool shouldQueue = false;
            bool shouldCrossfade = false;

            // Condition 1: Player is stopped, and queue is empty. Let's start the music.
            if (!playerState.IsPlaying && _queueService.IsQueueEmpty())
            {
                shouldQueue = true;
            }
            // Condition 2: A track is playing and nearing its end (5 seconds left)
            else if (playerState.IsPlaying && playerState.TimeRemaining.TotalSeconds < EndOfTrackThresholdSeconds)
            {
                // Only trigger if we haven't already queued for this track
                if (playerState.CurrentMediaId.HasValue && playerState.CurrentMediaId != _lastQueuedMediaId)
                {
                    shouldQueue = true;
                    shouldCrossfade = true;
                }
            }

            if (shouldQueue)
            {
                QueueNextTrack(playerState.CurrentMediaId);
            }

            // Notify UI to crossfade if needed
            if (shouldCrossfade)
            {
                CrossfadeRequested?.Invoke();
            }
        }

        private void QueueNextTrack(int? currentMediaId)
        {
            var activeRotation = _scheduler.GetActiveRotation();
            if (activeRotation == null)
            {
                ActiveRotationName = "None";
                _logger.LogWarning("No active or default rotation found");
                return;
            }

            ActiveRotationName = activeRotation.Name;
            var nextTrack = _rotationEngine.GetNextTrack(activeRotation);

            if (nextTrack != null)
            {
                _queueService.EnqueueTrack(nextTrack);
                _lastQueuedMediaId = currentMediaId; // Mark that we've queued for this ending track
                _logger.LogInformation("Queued track '{Title}' from rotation '{RotationName}'", nextTrack.Title, activeRotation.Name);
            }
            else
            {
                _logger.LogWarning("Rotation engine returned no track for rotation '{RotationName}'", activeRotation.Name);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
