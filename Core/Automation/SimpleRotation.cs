using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class SimpleRotation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public List<string> CategoryNames { get; set; } = new(); // Legacy: category names from LibraryService
        public List<string> CategoryIds { get; set; } = new(); // Legacy: category IDs for rotation (stringified Guid)
        public List<SimpleRotationSlot> Slots { get; set; } = new(); // New: ordered clockwheel slots
        public bool Enabled { get; set; } = true;
        public bool IsActive { get; set; } = false; // Only one rotation may be active at a time
        public int SortOrder { get; set; } = 0;

        // UI helper for comma-separated categories
        public string CategoryNamesString
        {
            get => string.Join(", ", CategoryNames ?? new());
            set => CategoryNames = (value ?? "").Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }
    }

    public sealed class SimpleRotationSlot
    {
        public Guid CategoryId { get; set; } = Guid.Empty;
        public int Weight { get; set; } = 1; // reserved for future weighting
        public string? CategoryName { get; set; } // legacy compatibility
    }

    public sealed class SimpleSchedulerEntry
    {
        public Guid RotationId { get; set; } = Guid.Empty;
        public string RotationName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
        public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(24);
        public bool Enabled { get; set; } = true;
        public DayOfWeek? Day { get; set; } // null = every day

        // UI helpers for time fields
        public string StartTimeString
        {
            get => StartTime.ToString(@"hh\:mm");
            set { if (TimeSpan.TryParse(value, out var t)) StartTime = t; }
        }
        public string EndTimeString
        {
            get => EndTime.ToString(@"hh\:mm");
            set { if (TimeSpan.TryParse(value, out var t)) EndTime = t; }
        }
    }
}