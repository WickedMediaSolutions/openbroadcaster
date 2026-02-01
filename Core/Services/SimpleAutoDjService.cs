using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Automation
{
    /// <summary>
    /// AutoDJ service responsible for maintaining queue depth with proper event-driven architecture.
    /// When enabled, AutoDJ is the single authority for keeping the queue populated.
    /// </summary>
    public sealed class SimpleAutoDjService : IDisposable
    {
        private static readonly Guid LibraryPseudoCategoryId = new("00000000-0000-0000-0000-000000000010");
        private static readonly Guid UncategorizedPseudoCategoryId = new("00000000-0000-0000-0000-000000000011");
        // CRITICAL CONSTANT: Minimum queue depth AutoDJ must maintain
        private const int MIN_QUEUE_DEPTH = 5;

        private readonly QueueService _queueService;
        private readonly LibraryService _libraryService;
        private readonly List<SimpleRotation> _rotations;
        private readonly List<SimpleSchedulerEntry> _schedule;
        private readonly int _targetQueueDepth;
        private Guid _defaultRotationId;
        private readonly System.Threading.Timer _timer;
        private readonly ILogger<SimpleAutoDjService> _logger;
        private bool _enabled;
        private string _status = "AutoDJ offline.";
        private readonly object _lock = new();
        private bool _isFilling = false;

        // Runtime state for rotation tracking
        private readonly Dictionary<Guid, int> _rotationSlotPointers = new();
        private readonly Dictionary<Guid, Queue<Track>> _categoryShuffleBags = new();
        private readonly Random _rng = new();
        private Track? _lastAddedTrack = null;
        private Guid? _currentRotationId = null;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler? QueueFilled;

        public IReadOnlyList<SimpleRotation> Rotations => _rotations;

        public SimpleAutoDjService(
            QueueService queueService,
            LibraryService libraryService,
            List<SimpleRotation> rotations,
            List<SimpleSchedulerEntry> schedule,
            int targetQueueDepth = 5,
            Guid defaultRotationId = default,
            ILogger<SimpleAutoDjService>? logger = null)
        {
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _rotations = rotations ?? new List<SimpleRotation>();
            _schedule = schedule ?? new List<SimpleSchedulerEntry>();
            _targetQueueDepth = Math.Max(MIN_QUEUE_DEPTH, targetQueueDepth);
            _defaultRotationId = defaultRotationId;
            _timer = new System.Threading.Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
            _logger = logger ?? AppLogger.CreateLogger<SimpleAutoDjService>();

            // Subscribe to QueueChanged event for event-driven queue filling
            _queueService.QueueChanged += OnQueueChanged;
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                if (_enabled)
                {
                    _logger.LogInformation("AutoDJ enabled. Rotations available: {Count}", _rotations?.Count ?? 0);
                    if (_rotations != null && _rotations.Count > 0)
                    {
                        var activeCount = _rotations.Count(r => r.IsActive);
                        var enabledCount = _rotations.Count(r => r.Enabled);
                        _logger.LogDebug("Active rotations: {Active}, Enabled rotations: {Enabled}", activeCount, enabledCount);
                    }

                    // Ensure the correct rotation is active before filling the queue
                    UpdateActiveRotationIfNeeded();

                    _timer.Change(0, 5000); // Check every 5 seconds as backup
                    UpdateStatus("AutoDJ enabled.");
                    _logger.LogDebug("Starting initial queue fill");
                    EnsureQueueDepth();
                }
                else
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    UpdateStatus("AutoDJ paused.");
                    _logger.LogInformation("AutoDJ disabled");
                }
            }
        }

        public string Status => _status;

        public void Dispose()
        {
            _queueService.QueueChanged -= OnQueueChanged;
            _timer.Dispose();
        }

        /// <summary>
        /// Event handler for QueueChanged event - ensures queue depth is maintained
        /// Queue is always kept populated regardless of AutoDJ enabled state
        /// </summary>
        private void OnQueueChanged(object? sender, EventArgs e)
        {
            // Don't refill if we're already filling - prevents recursive calls
            if (_isFilling)
            {
                return;
            }
            
            _logger.LogDebug("Queue changed event received - checking depth");
            // Run on background thread to avoid UI freezes
            System.Threading.Tasks.Task.Run(() => EnsureQueueDepth());
        }

        /// <summary>
        /// Timer callback - backup mechanism to ensure queue depth
        /// </summary>
        private void OnTimer(object? state)
        {
            // When AutoDJ is running, keep the active rotation in sync with the schedule
            if (_enabled)
            {
                UpdateActiveRotationIfNeeded();
            }

            // Always ensure queue depth, regardless of enabled state
            EnsureQueueDepth();
        }

        /// <summary>
        /// Updates the active rotation based on the current schedule/defaults.
        /// If the active rotation changes while AutoDJ is running, the queue is
        /// cleared and repopulated from the new rotation.
        /// </summary>
        public void UpdateActiveRotationIfNeeded()
        {
            SimpleRotation? newRotation = null;
            Guid? newRotationId = null;

            lock (_lock)
            {
                if (_rotations == null || _rotations.Count == 0)
                {
                    return;
                }

                var now = DateTime.Now;
                var scheduledId = GetScheduledRotationId(now);

                // Log scheduler evaluation details at debug level so we can
                // diagnose why a configured schedule might not be taking
                // effect at runtime.
                _logger.LogDebug(
                    "[AutoDJ] Schedule check at {NowTime} (entries={EntryCount}, scheduledRotationId={ScheduledId})",
                    now.ToString("HH:mm:ss"),
                    _schedule?.Count ?? 0,
                    scheduledId.HasValue && scheduledId.Value != Guid.Empty ? scheduledId.Value : null);

                // Selection precedence:
                //  1. Matching schedule entry (if any)
                //  2. Currently active rotation (if still valid)
                //  3. Default rotation from settings (if valid)
                //  4. First enabled rotation, or the first rotation.
                if (scheduledId.HasValue && scheduledId.Value != Guid.Empty)
                {
                    newRotationId = scheduledId.Value;
                }
                else if (_currentRotationId.HasValue && _rotations.Exists(r => r.Id == _currentRotationId.Value))
                {
                    newRotationId = _currentRotationId.Value;
                }
                else if (_defaultRotationId != Guid.Empty && _rotations.Exists(r => r.Id == _defaultRotationId))
                {
                    newRotationId = _defaultRotationId;
                }
                else
                {
                    var enabled = _rotations.Find(r => r.Enabled);
                    newRotationId = (enabled ?? _rotations[0]).Id;
                }

                if (_currentRotationId.HasValue && _currentRotationId.Value == newRotationId.Value)
                {
                    // No change in active rotation
                    _logger.LogDebug(
                        "[AutoDJ] Active rotation unchanged after schedule check (RotationId={RotationId})",
                        newRotationId);
                    return;
                }

                _currentRotationId = newRotationId;

                foreach (var rotation in _rotations)
                {
                    rotation.IsActive = rotation.Id == newRotationId.Value;
                }

                // Reset rotation state so the new rotation starts cleanly
                _rotationSlotPointers.Clear();
                _categoryShuffleBags.Clear();
                _lastAddedTrack = null;

                newRotation = _rotations.Find(r => r.Id == newRotationId.Value);
                _logger.LogInformation("Active AutoDJ rotation switched to '{RotationName}' (Id={RotationId})",
                    newRotation?.Name ?? "Unknown", newRotationId);
            }

            // Outside the lock: clear and refill the queue so that
            // the new rotation takes effect immediately.
            if (_enabled)
            {
                _queueService.Clear();
                EnsureQueueDepth();
            }
        }

        /// <summary>
        /// Replaces the in-memory rotations, schedule, and default rotation used
        /// by this AutoDJ service. This is called when settings are applied so
        /// that changes take effect without restarting the app.
        /// </summary>
        public void UpdateConfiguration(System.Collections.Generic.List<SimpleRotation> rotations,
                                         System.Collections.Generic.List<SimpleSchedulerEntry> schedule,
                                         Guid defaultRotationId)
        {
            if (rotations == null) throw new ArgumentNullException(nameof(rotations));
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            lock (_lock)
            {
                _rotations.Clear();
                _rotations.AddRange(rotations);

                _schedule.Clear();
                _schedule.AddRange(schedule);

                _defaultRotationId = defaultRotationId;

                // Reset runtime rotation state so new configuration starts clean
                _rotationSlotPointers.Clear();
                _categoryShuffleBags.Clear();
                _lastAddedTrack = null;
            }

            // Re-evaluate active rotation with the new configuration
            UpdateActiveRotationIfNeeded();
        }

        /// <summary>
        /// Manually sets the active rotation used for queue filling.
        /// Intended for live-assist use when AutoDJ is not enabled.
        /// Does not clear the existing queue; new fills will use the
        /// selected rotation going forward.
        /// </summary>
        public void SetManualActiveRotation(Guid rotationId)
        {
            lock (_lock)
            {
                if (_rotations == null || _rotations.Count == 0)
                {
                    return;
                }

                var rotation = _rotations.FirstOrDefault(r => r.Id == rotationId);
                if (rotation == null)
                {
                    return;
                }

                foreach (var r in _rotations)
                {
                    r.IsActive = r.Id == rotationId;
                }

                _currentRotationId = rotationId;
                _rotationSlotPointers.Clear();
                _categoryShuffleBags.Clear();
                _lastAddedTrack = null;

                _logger.LogInformation("Manual live-assist rotation set to '{RotationName}' (Id={RotationId})",
                    rotation.Name, rotationId);
            }
        }

        /// <summary>
        /// Determines which rotation should be active at the specified time
        /// using the simple schedule entries. Returns null when no schedule
        /// entry matches, allowing callers to fall back to defaults.
        /// </summary>
        private Guid? GetScheduledRotationId(DateTime now)
        {
            if (_schedule == null || _schedule.Count == 0)
            {
                return null;
            }

            var timeOfDay = now.TimeOfDay;
            var today = now.DayOfWeek;
            SimpleSchedulerEntry? best = null;

            foreach (var entry in _schedule)
            {
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                if (entry.Day.HasValue && entry.Day.Value != today)
                {
                    continue;
                }

                var start = entry.StartTime;
                var end = entry.EndTime;

                bool inRange;
                if (end <= start)
                {
                    // Overnight slot (e.g., 22:00–02:00)
                    inRange = timeOfDay >= start || timeOfDay < end;
                }
                else
                {
                    inRange = timeOfDay >= start && timeOfDay < end;
                }

                if (!inRange)
                {
                    continue;
                }

                if (best == null || entry.StartTime > best.StartTime)
                {
                    best = entry;
                }
            }

            if (best == null)
            {
                return null;
            }

            // Prefer explicit RotationId; fall back to matching by name
            if (best.RotationId != Guid.Empty && _rotations.Exists(r => r.Id == best.RotationId))
            {
                return best.RotationId;
            }

            if (!string.IsNullOrWhiteSpace(best.RotationName))
            {
                var byName = _rotations.Find(r => string.Equals(r.Name, best.RotationName, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                {
                    return byName.Id;
                }
            }

            return null;
        }

        /// <summary>
        /// Core queue filling logic - ensures queue never drops below MIN_QUEUE_DEPTH
        /// Thread-safe and prevents concurrent fills
        /// This ALWAYS works regardless of Enabled state - Enabled only controls automatic refilling
        /// </summary>
        public void EnsureQueueDepth()
        {
            // Prevent concurrent fills
            lock (_lock)
            {
                if (_isFilling)
                {
                    _logger.LogDebug("Queue fill already in progress, skipping");
                    return;
                }
                _isFilling = true;
            }

            try
            {
                var snapshot = _queueService.Snapshot();
                var currentDepth = snapshot.Count;
                _logger.LogDebug("Queue depth check: {Current}/{Target} tracks (MIN={Min})", currentDepth, _targetQueueDepth, MIN_QUEUE_DEPTH);

                // Only fill if below target depth
                while (currentDepth < _targetQueueDepth)
                {
                    var track = GetNextTrack();
                    if (track == null)
                    {
                        _logger.LogWarning("Failed to get next track - stopping fill");
                        break;
                    }

                    // Get active rotation info for metadata
                    var activeRotation = GetActiveRotation();
                    var rotationName = activeRotation?.Name ?? "Unknown";
                    
                    // Determine category name from track's first category ID
                    var categoryName = "Unknown";
                    if (track.CategoryIds != null && track.CategoryIds.Count > 0)
                    {
                        var firstCatId = track.CategoryIds.First();
                        var category = _libraryService.GetCategories().FirstOrDefault(c => c.Id == firstCatId);
                        categoryName = category?.Name ?? "Unknown";
                    }

                    var queueItem = new QueueItem(
                        track, 
                        QueueSource.AutoDj, 
                        "AutoDJ", 
                        string.Empty,  // No requester for AutoDJ tracks
                        rotationName,
                        categoryName
                    );

                    _queueService.Enqueue(queueItem);
                    _lastAddedTrack = track;
                    currentDepth++;
                    _logger.LogDebug("Added track: {Title} by {Artist} (Category: {Category}, Rotation: {Rotation})", track.Title, track.Artist, categoryName, rotationName);
                }

                if (currentDepth >= MIN_QUEUE_DEPTH)
                {
                    var statusPrefix = _enabled ? "AutoDJ active" : "Queue ready";
                    UpdateStatus($"{statusPrefix}. Queue: {currentDepth} tracks.");
                }
                else
                {
                    var statusPrefix = _enabled ? "AutoDJ active" : "Queue";
                    UpdateStatus($"{statusPrefix}. Queue low: {currentDepth} tracks.");
                }
            }
            finally
            {
                lock (_lock)
                {
                    _isFilling = false;
                }
            }

            QueueFilled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Get preview of upcoming tracks without affecting rotation state
        /// </summary>
        public List<Track> GetUpcomingPreview(int count)
        {
            lock (_lock)
            {
                var result = new List<Track>();

                // Store current state for rotation pointers and shuffle bags
                var savedPointers = new Dictionary<Guid, int>(_rotationSlotPointers);
                var savedBags = new Dictionary<Guid, Queue<Track>>();
                foreach (var kvp in _categoryShuffleBags)
                {
                    // Clone each shuffle bag so we can simulate draws safely
                    savedBags[kvp.Key] = new Queue<Track>(kvp.Value);
                }

                // Generate preview using the same rotation/shuffle logic
                for (int i = 0; i < count; i++)
                {
                    var track = GetNextTrack();
                    if (track != null)
                    {
                        result.Add(track);
                    }
                }

                // Restore runtime state exactly as it was
                _rotationSlotPointers.Clear();
                foreach (var kvp in savedPointers)
                {
                    _rotationSlotPointers[kvp.Key] = kvp.Value;
                }

                _categoryShuffleBags.Clear();
                foreach (var kvp in savedBags)
                {
                    _categoryShuffleBags[kvp.Key] = kvp.Value;
                }

                return result;
            }
        }

        /// <summary>
        /// Get the next track from the active rotation
        /// Walks rotation slot-by-slot, prevents back-to-back duplicates
        /// </summary>
        private Track? GetNextTrack()
        {
            lock (_lock)
            {
                var rotation = GetActiveRotation();
                if (rotation == null)
                {
                    _logger.LogWarning("No active rotation found");
                    return null;
                }

                if (!rotation.Enabled)
                {
                    _logger.LogWarning("Active rotation '{RotationName}' is disabled", rotation.Name);
                    return null;
                }

                // Prefer slot-based clockwheel; fall back to legacy category lists
                var slots = rotation.Slots != null && rotation.Slots.Count > 0
                    ? rotation.Slots
                    : BuildLegacySlots(rotation);

                if (slots.Count == 0)
                {
                    _logger.LogWarning("Rotation '{RotationName}' has no slots", rotation.Name);
                    return null;
                }

                // Initialize pointer for rotation
                if (!_rotationSlotPointers.ContainsKey(rotation.Id))
                {
                    _rotationSlotPointers[rotation.Id] = 0;
                }

                var attempts = 0;
                var maxAttempts = slots.Count * 2; // Try twice around the rotation
                var slotIndex = _rotationSlotPointers[rotation.Id];

                while (attempts < maxAttempts)
                {
                    var slot = slots[slotIndex];
                    var track = TryPickTrackForSlot(slot);

                    // Advance pointer for next call
                    slotIndex = (slotIndex + 1) % slots.Count;
                    attempts++;

                    if (track != null)
                    {
                        // Prevent immediate back-to-back repeats
                        if (_lastAddedTrack != null && track.Id == _lastAddedTrack.Id && attempts < maxAttempts)
                        {
                            _logger.LogDebug("Skipping duplicate track: {Title}", track.Title);
                            continue;
                        }

                        _rotationSlotPointers[rotation.Id] = slotIndex;
                        return track;
                    }
                }

                // No track found after full rotation
                _logger.LogError("No valid tracks found after full rotation '{RotationName}'", rotation.Name);
                return null;
            }
        }

        /// <summary>
        /// Get the currently active rotation based on schedule or default
        /// Only returns rotations where IsActive=true
        /// </summary>
        private SimpleRotation? GetActiveRotation()
        {
            lock (_lock)
            {
                if (_rotations == null || _rotations.Count == 0)
                {
                    _logger.LogWarning("No rotations available");
                    return null;
                }

                // CRITICAL: Only use rotations marked as IsActive
                var activeRotation = _rotations.FirstOrDefault(r => r.IsActive && r.Enabled);

                if (activeRotation != null)
                {
                    return activeRotation;
                }

                // No active rotation found - try to auto-activate first enabled rotation
                var firstEnabled = _rotations.FirstOrDefault(r => r.Enabled);
                if (firstEnabled != null)
                {
                    _logger.LogWarning("No active rotation found. Auto-activating '{RotationName}'", firstEnabled.Name);
                    firstEnabled.IsActive = true;
                    return firstEnabled;
                }

                // Still nothing - try any rotation
                var anyRotation = _rotations.FirstOrDefault();
                if (anyRotation != null)
                {
                    _logger.LogWarning("No enabled rotations. Auto-activating '{RotationName}'", anyRotation.Name);
                    anyRotation.IsActive = true;
                    anyRotation.Enabled = true;
                    return anyRotation;
                }

                _logger.LogError("No rotations available at all. Please create a rotation in settings.");
                return null;
            }
        }

        /// <summary>
        /// Build legacy slots from CategoryIds or CategoryNames for backward compatibility
        /// </summary>
        private List<SimpleRotationSlot> BuildLegacySlots(SimpleRotation rotation)
        {
            var slots = new List<SimpleRotationSlot>();
            if (rotation.CategoryIds != null && rotation.CategoryIds.Count > 0)
            {
                foreach (var idString in rotation.CategoryIds)
                {
                    if (Guid.TryParse(idString, out var gid))
                    {
                        slots.Add(new SimpleRotationSlot { CategoryId = gid });
                    }
                }
            }
            else if (rotation.CategoryNames != null && rotation.CategoryNames.Count > 0)
            {
                foreach (var name in rotation.CategoryNames)
                {
                    slots.Add(new SimpleRotationSlot { CategoryId = Guid.Empty, CategoryName = name });
                }
            }
            return slots;
        }

        /// <summary>
        /// Try to pick a random track from a slot's category
        /// Uses shuffle bag pattern to prevent immediate repeats within a category
        /// </summary>
        private Track? TryPickTrackForSlot(SimpleRotationSlot slot)
        {
            var categoryId = slot.CategoryId;

            // Legacy: resolve category by name if Guid is missing
            if (categoryId == Guid.Empty && !string.IsNullOrWhiteSpace(slot.CategoryName))
            {
                if (string.Equals(slot.CategoryName, "Library", StringComparison.OrdinalIgnoreCase))
                {
                    categoryId = LibraryPseudoCategoryId;
                    slot.CategoryId = categoryId;
                }
                else if (string.Equals(slot.CategoryName, "Uncategorized", StringComparison.OrdinalIgnoreCase))
                {
                    categoryId = UncategorizedPseudoCategoryId;
                    slot.CategoryId = categoryId;
                }

                if (categoryId != Guid.Empty)
                {
                    // continue with resolved pseudo category
                }
                else
                {
                var cat = _libraryService.GetCategories().FirstOrDefault(c => 
                    string.Equals(c.Name, slot.CategoryName, StringComparison.OrdinalIgnoreCase));
                if (cat != null)
                {
                    categoryId = cat.Id;
                    slot.CategoryId = cat.Id; // cache for next time
                }
                else
                {
                    _logger.LogWarning("Category '{CategoryName}' not found in library", slot.CategoryName);
                    return null;
                }
                }
            }

            if (categoryId == Guid.Empty)
            {
                _logger.LogWarning("Slot has no valid category ID");
                return null;
            }

            // Get or refresh shuffle bag for this category
            if (!_categoryShuffleBags.TryGetValue(categoryId, out var bag) || bag.Count == 0)
            {
                IReadOnlyCollection<Track> allTracksInCategory = categoryId switch
                {
                    var id when id == LibraryPseudoCategoryId => _libraryService.GetAllTracks(),
                    var id when id == UncategorizedPseudoCategoryId => _libraryService.GetUncategorizedTracks(),
                    _ => _libraryService.GetTracksByCategory(categoryId)
                };
                var fresh = allTracksInCategory
                    .Where(t => t != null && t.IsEnabled)
                    .OrderBy(_ => _rng.Next())
                    .ToList();

                if (fresh.Count == 0)
                {
                    _logger.LogWarning("No enabled tracks in category {CategoryId}. Total tracks in category: {TotalTracks}", categoryId, allTracksInCategory?.Count ?? 0);
                    return null;
                }

                bag = new Queue<Track>(fresh);
                _categoryShuffleBags[categoryId] = bag;
                _logger.LogDebug("Loaded {Count} tracks for category {CategoryId}", fresh.Count, categoryId);
            }

            return bag.Dequeue();
        }

        private void UpdateStatus(string message)
        {
            _status = message;
            StatusChanged?.Invoke(this, message);
        }
    }
}
