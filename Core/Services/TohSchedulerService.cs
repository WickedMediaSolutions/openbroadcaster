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
        private int _lastFiredHalfHour = -1;
        private bool _isAutoDjRunning;
        private bool _disposed;
        private bool _shouldFireToh;
        private bool _shouldFireBoh;

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
                _lastFiredHalfHour = _settings.LastFiredHalfHour;

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

                _logger.LogInformation("TOH settings updated: Enabled={Enabled}, Slots={SlotCount}, Offset={Offset}s, BOH={BohEnabled}",
                    _settings.Enabled, _settings.Slots?.Count ?? 0, _settings.FireSecondOffset, _settings.BohEnabled);
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
            bool shouldExecuteToh = false;
            bool shouldExecuteBoh = false;

            lock (_lock)
            {
                _shouldFireToh = false;
                _shouldFireBoh = false;

                var now = DateTime.Now;
                var currentHour = now.Hour;
                var currentMinute = now.Minute;
                var currentSecond = now.Second;

                // Check Top-of-Hour (at minute 0)
                if (_settings.Enabled && _settings.Slots?.Count > 0)
                {
                    var targetSecond = _settings.FireSecondOffset;
                    if (targetSecond < 0) targetSecond = 0;
                    if (targetSecond > 59) targetSecond = 59;

                    if (currentMinute == 0 && currentSecond == targetSecond && _lastFiredHour != currentHour)
                    {
                        // Check mode restrictions for TOH
                        if ((!_isAutoDjRunning || _settings.AllowDuringAutoDj) &&
                            (_isAutoDjRunning || _settings.AllowDuringLiveAssist))
                        {
                            _lastFiredHour = currentHour;
                            _shouldFireToh = true;
                            shouldExecuteToh = true;
                            _logger.LogInformation("TOH condition met at {Time}", now);
                        }
                        else
                        {
                            _logger.LogDebug("TOH skipped due to mode restrictions at {Time}", now);
                        }
                    }
                }

                // Check Bottom-of-Hour (at minute 30)
                if (_settings.BohEnabled && _settings.BohSlots?.Count > 0)
                {
                    var targetSecond = _settings.BohFireSecondOffset;
                    if (targetSecond < 0) targetSecond = 0;
                    if (targetSecond > 59) targetSecond = 59;

                    var halfHourId = currentHour * 2 + (currentMinute >= 30 ? 1 : 0);

                    if (currentMinute == 30 && currentSecond == targetSecond && _lastFiredHalfHour != halfHourId)
                    {
                        // Check mode restrictions for BOH
                        if ((!_isAutoDjRunning || _settings.BohAllowDuringAutoDj) &&
                            (_isAutoDjRunning || _settings.BohAllowDuringLiveAssist))
                        {
                            _lastFiredHalfHour = halfHourId;
                            _shouldFireBoh = true;
                            shouldExecuteBoh = true;
                            _logger.LogInformation("BOH condition met at {Time}", now);
                        }
                        else
                        {
                            _logger.LogDebug("BOH skipped due to mode restrictions at {Time}", now);
                        }
                    }
                }
            }

            // Execute outside lock to avoid deadlocks
            if (shouldExecuteToh || shouldExecuteBoh)
            {
                ExecuteTohInjection(force: false);
            }
        }

        private int ExecuteTohInjection(bool force)
        {
            List<TohSlot> tohSlots;
            List<TohSlot> bohSlots;
            bool fireToh;
            bool fireBoh;

            lock (_lock)
            {
                fireToh = force || _shouldFireToh;
                fireBoh = force || _shouldFireBoh;

                // Reset flags
                _shouldFireToh = false;
                _shouldFireBoh = false;

                // TOH injection
                if (fireToh && _settings.Enabled)
                {
                    tohSlots = _settings.Slots?.OrderBy(s => s.SlotOrder).ToList() ?? new List<TohSlot>();
                }
                else
                {
                    tohSlots = new List<TohSlot>();
                }

                // BOH injection
                if (fireBoh && _settings.BohEnabled)
                {
                    bohSlots = _settings.BohSlots?.OrderBy(s => s.SlotOrder).ToList() ?? new List<TohSlot>();
                }
                else
                {
                    bohSlots = new List<TohSlot>();
                }
            }

            int insertedCount = 0;

            // Execute TOH injection
            if (tohSlots.Count > 0)
            {
                insertedCount += ExecuteInjection(tohSlots, "Top of Hour", QueueSource.TopOfHour);
            }

            // Execute BOH injection
            if (bohSlots.Count > 0)
            {
                insertedCount += ExecuteInjection(bohSlots, "Bottom of Hour", QueueSource.TopOfHour); // BOH uses TopOfHour source for now
            }

            return insertedCount;
        }

        private int ExecuteInjection(List<TohSlot> slots, string injectionType, QueueSource source)
        {
            if (slots.Count == 0)
            {
                _logger.LogWarning("{Type} triggered but no slots configured", injectionType);
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
                _logger.LogWarning("{Type} triggered but no tracks selected from any slot", injectionType);
                StatusChanged?.Invoke(this, $"{injectionType}: No tracks available");
                return 0;
            }

            // Insert tracks at the top of the queue in reverse order so they maintain order
            var insertedCount = 0;
            for (int i = tracksToInsert.Count - 1; i >= 0; i--)
            {
                var track = tracksToInsert[i];
                var queueItem = new QueueItem(track, source, injectionType, string.Empty);
                _queueService.EnqueueFront(queueItem);
                insertedCount++;
                _logger.LogInformation("{Type} inserted: {Title} by {Artist}", injectionType, track.Title, track.Artist);
            }

            // Log warnings
            foreach (var warning in warnings)
            {
                _logger.LogWarning("{Type}: {Warning}", injectionType, warning);
            }

            var message = $"{injectionType}: Inserted {insertedCount} track(s)";
            StatusChanged?.Invoke(this, message);
            TohFired?.Invoke(this, new TohFiredEventArgs(insertedCount, tracksToInsert.Select(t => t.Title).ToList()));

            _logger.LogInformation("{Type} fired successfully: {Count} tracks inserted", injectionType, insertedCount);
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
