using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class RotationProgram
    {
        public RotationProgram(string name, IEnumerable<ClockwheelSlot> slots)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Rotation" : name.Trim();
            Slots = slots?.Where(static slot => slot != null).ToList() ?? new List<ClockwheelSlot>();
        }

        public string Name { get; }
        public IReadOnlyList<ClockwheelSlot> Slots { get; }

        internal bool HasSlots => Slots.Count > 0;
    }

    public sealed class RotationScheduleEntry
    {
        public RotationScheduleEntry(DayOfWeek day, TimeSpan startTime, string rotationName)
        {
            Day = day;
            StartTime = startTime < TimeSpan.Zero ? TimeSpan.Zero : startTime;
            RotationName = rotationName?.Trim() ?? string.Empty;
        }

        public DayOfWeek Day { get; }
        public TimeSpan StartTime { get; }
        public string RotationName { get; }
    }
}
