using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// Background service that monitors system time and injects Top-of-the-Hour tracks at the configured time.
    /// </summary>
    public sealed class TohSchedulerService : IDisposable
    {
        private readonly QueueService _queueService;
        private readonly LibraryService _libraryService;
        private readonly ILogger<TohSchedulerService> _logger;
        private readonly object _lock = new();
        private readonly Random _random = new();
        private readonly Dictionary<Guid, int> _sequentialIndices = new();
        private readonly HashSet<Guid> _recentlyPlayed = new();

        private System.Threading.Timer? _timer;
        private TohSettings _settings = new();
        private int _lastFiredHour = -1;
        private bool _isAutoDjRunning;
        private bool _disposed;

        /// <summary>
        /// Raised when TOH fires and inserts tracks.
        /// </summary>
        public event EventHandler<TohFiredEventArgs>? TohFired;

        /// <summary>
        /// Raised when TOH status changes.
        /// </summary>
        public event EventHandler<string>? StatusChanged;

        public TohSchedulerService(
            QueueService queueService,
            LibraryService libraryService,
            ILogger<TohSchedulerService>? logger = null)
        {
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _logger = logger ?? AppLogger.CreateLogger<TohSchedulerService>();
        }

        /// <summary>
        /// Gets whether TOH is enabled and has slots configured.
        /// </summary>
        public bool IsActive
        {
            get
            {
                lock (_lock)
                {
                    return _settings.Enabled && _settings.Slots.Count > 0;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether AutoDJ is currently running.
        /// </summary>
        public bool IsAutoDjRunning
        {
            get { lock (_lock) return _isAutoDjRunning; }
            set { lock (_lock) _isAutoDjRunning = value; }
        }

        /// <summary>
        /// Starts the TOH scheduler background timer.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_timer != null)
                {
                    return;
                }

                // Check every second for the top of the hour
                _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
                _logger.LogInformation("TOH scheduler started");
                StatusChanged?.Invoke(this, "TOH scheduler started");
            }
        }

        /// <summary>
        /// Stops the TOH scheduler.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                _logger.LogInformation("TOH scheduler stopped");
                StatusChanged?.Invoke(this, "TOH scheduler stopped");
            }
        }

        /// <summary>
        /// Updates TOH settings.
        /// </summary>
        public void UpdateSettings(TohSettings settings)
        {
            lock (_lock)
            {
                _settings = settings?.Clone() ?? new TohSettings();
                _lastFiredHour = _settings.LastFiredHour;

                // Restore sequential indices
                _sequentialIndices.Clear();
                if (_settings.SequentialIndices != null)
                {
                    foreach (var idx in _settings.SequentialIndices)
                    {
                        if (Guid.TryParse(idx.CategoryId, out var catId))
                        {
                            _sequentialIndices[catId] = idx.LastIndex;
                        }
                    }
                }

                _logger.LogInformation("TOH settings updated: Enabled={Enabled}, Slots={SlotCount}, Offset={Offset}s",
                    _settings.Enabled, _settings.Slots?.Count ?? 0, _settings.FireSecondOffset);
            }
        }

        /// <summary>
        /// Gets the current sequential indices for persistence.
        /// </summary>
        public IReadOnlyList<TohSequentialIndex> GetSequentialIndices()
        {
            lock (_lock)
            {
                return _sequentialIndices.Select(kvp => new TohSequentialIndex
                {
                    CategoryId = kvp.Key.ToString(),
                    LastIndex = kvp.Value
                }).ToList();
            }
        }

        /// <summary>
        /// Manually triggers TOH injection (for testing or manual override).
        /// </summary>
        public int ManualTrigger()
        {
            return ExecuteTohInjection(force: true);
        }

        private void OnTimerTick(object? state)
        {
            try
            {
                CheckAndFireToh();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TOH timer tick");
            }
        }

        private void CheckAndFireToh()
        {
            lock (_lock)
            {
                if (!_settings.Enabled || _settings.Slots == null || _settings.Slots.Count == 0)
                {
                    return;
                }

                var now = DateTime.Now;
                var currentHour = now.Hour;
                var currentMinute = now.Minute;
                var currentSecond = now.Second;

                // Check if we're at the top of the hour (with offset)
                var targetSecond = _settings.FireSecondOffset;
                if (targetSecond < 0) targetSecond = 0;
                if (targetSecond > 59) targetSecond = 59;

                // Fire at minute 0, second = targetSecond
                if (currentMinute != 0 || currentSecond != targetSecond)
                {
                    return;
                }

                // Check if we already fired this hour
                if (_lastFiredHour == currentHour)
                {
                    return;
                }

                // Check mode restrictions
                if (_isAutoDjRunning && !_settings.AllowDuringAutoDj)
                {
                    _logger.LogDebug("TOH skipped: AutoDJ running and AllowDuringAutoDj=false");
                    return;
                }

                if (!_isAutoDjRunning && !_settings.AllowDuringLiveAssist)
                {
                    _logger.LogDebug("TOH skipped: Live assist mode and AllowDuringLiveAssist=false");
                    return;
                }

                // Fire TOH
                _lastFiredHour = currentHour;
            }

            ExecuteTohInjection(force: false);
        }

        private int ExecuteTohInjection(bool force)
        {
            List<TohSlot> slots;
            lock (_lock)
            {
                if (!force && !_settings.Enabled)
                {
                    return 0;
                }

                slots = _settings.Slots?.OrderBy(s => s.SlotOrder).ToList() ?? new List<TohSlot>();
            }

            if (slots.Count == 0)
            {
                _logger.LogWarning("TOH triggered but no slots configured");
                return 0;
            }

            var tracksToInsert = new List<Track>();
            var warnings = new List<string>();

            foreach (var slot in slots)
            {
                var selectedTracks = SelectTracksForSlot(slot, warnings);
                tracksToInsert.AddRange(selectedTracks);
            }

            if (tracksToInsert.Count == 0)
            {
                _logger.LogWarning("TOH triggered but no tracks selected from any slot");
                StatusChanged?.Invoke(this, "TOH: No tracks available");
                return 0;
            }

            // Insert tracks at the top of the queue in reverse order so they maintain order
            var insertedCount = 0;
            for (int i = tracksToInsert.Count - 1; i >= 0; i--)
            {
                var track = tracksToInsert[i];
                var queueItem = new QueueItem(track, QueueSource.TopOfHour, "Top of Hour", string.Empty);
                _queueService.EnqueueFront(queueItem);
                insertedCount++;
                _logger.LogInformation("TOH inserted: {Title} by {Artist}", track.Title, track.Artist);
            }

            // Log warnings
            foreach (var warning in warnings)
            {
                _logger.LogWarning("TOH: {Warning}", warning);
            }

            var message = $"TOH: Inserted {insertedCount} track(s)";
            StatusChanged?.Invoke(this, message);
            TohFired?.Invoke(this, new TohFiredEventArgs(insertedCount, tracksToInsert.Select(t => t.Title).ToList()));

            _logger.LogInformation("TOH fired successfully: {Count} tracks inserted", insertedCount);
            return insertedCount;
        }

        private List<Track> SelectTracksForSlot(TohSlot slot, List<string> warnings)
        {
            var result = new List<Track>();

            if (slot.CategoryId == Guid.Empty)
            {
                warnings.Add($"Slot {slot.SlotOrder}: No category configured");
                return result;
            }

            var allTracks = _libraryService.GetTracksByCategory(slot.CategoryId)
                .Where(t => t.IsEnabled)
                .ToList();

            if (allTracks.Count == 0)
            {
                warnings.Add($"Slot {slot.SlotOrder} ({slot.CategoryName}): No enabled tracks in category");
                return result;
            }

            var requestedCount = Math.Max(1, slot.TrackCount);
            if (allTracks.Count < requestedCount)
            {
                warnings.Add($"Slot {slot.SlotOrder} ({slot.CategoryName}): Only {allTracks.Count} tracks available, requested {requestedCount}");
            }

            var countToSelect = Math.Min(requestedCount, allTracks.Count);

            if (slot.SelectionMode == TohSelectionMode.Sequential)
            {
                result = SelectSequential(slot.CategoryId, allTracks, countToSelect, slot.PreventRepeat);
            }
            else
            {
                result = SelectRandom(slot.CategoryId, allTracks, countToSelect, slot.PreventRepeat);
            }

            return result;
        }

        private List<Track> SelectSequential(Guid categoryId, List<Track> tracks, int count, bool preventRepeat)
        {
            var result = new List<Track>();
            
            lock (_lock)
            {
                if (!_sequentialIndices.TryGetValue(categoryId, out var startIndex))
                {
                    startIndex = 0;
                }

                for (int i = 0; i < count; i++)
                {
                    var index = (startIndex + i) % tracks.Count;
                    var track = tracks[index];
                    
                    if (preventRepeat && _recentlyPlayed.Contains(track.Id) && tracks.Count > count)
                    {
                        // Skip recently played, find next available
                        for (int j = 1; j < tracks.Count; j++)
                        {
                            var altIndex = (index + j) % tracks.Count;
                            var altTrack = tracks[altIndex];
                            if (!_recentlyPlayed.Contains(altTrack.Id))
                            {
                                track = altTrack;
                                index = altIndex;
                                break;
                            }
                        }
                    }

                    result.Add(track);
                    _recentlyPlayed.Add(track.Id);
                }

                // Update sequential index
                _sequentialIndices[categoryId] = (startIndex + count) % tracks.Count;

                // Keep recently played list manageable
                CleanupRecentlyPlayed();
            }

            return result;
        }

        private List<Track> SelectRandom(Guid categoryId, List<Track> tracks, int count, bool preventRepeat)
        {
            var result = new List<Track>();
            var available = new List<Track>(tracks);
            
            lock (_lock)
            {
                if (preventRepeat)
                {
                    available = tracks.Where(t => !_recentlyPlayed.Contains(t.Id)).ToList();
                    if (available.Count == 0)
                    {
                        // All tracks recently played, clear history and use all
                        _recentlyPlayed.Clear();
                        available = new List<Track>(tracks);
                    }
                }

                for (int i = 0; i < count && available.Count > 0; i++)
                {
                    var index = _random.Next(available.Count);
                    var track = available[index];
                    result.Add(track);
                    available.RemoveAt(index);
                    _recentlyPlayed.Add(track.Id);
                }

                CleanupRecentlyPlayed();
            }

            return result;
        }

        private void CleanupRecentlyPlayed()
        {
            // Keep only last 100 tracks in history
            if (_recentlyPlayed.Count > 100)
            {
                var toRemove = _recentlyPlayed.Take(_recentlyPlayed.Count - 100).ToList();
                foreach (var id in toRemove)
                {
                    _recentlyPlayed.Remove(id);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }
    }

    /// <summary>
    /// Event args for when TOH fires.
    /// </summary>
    public sealed class TohFiredEventArgs : EventArgs
    {
        public int TrackCount { get; }
        public IReadOnlyList<string> TrackTitles { get; }

        public TohFiredEventArgs(int trackCount, IReadOnlyList<string> trackTitles)
        {
            TrackCount = trackCount;
            TrackTitles = trackTitles;
        }
    }
}
