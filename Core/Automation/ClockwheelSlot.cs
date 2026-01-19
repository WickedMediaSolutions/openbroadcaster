using System;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class ClockwheelSlot
    {
        public ClockwheelSlot(string categoryName, Guid? trackId = null, string? displayLabel = null)
        {
            if (string.IsNullOrWhiteSpace(categoryName) && trackId == null)
            {
                throw new ArgumentException("Clockwheel slot must define a category or track.", nameof(categoryName));
            }

            CategoryName = categoryName?.Trim() ?? string.Empty;
            TrackId = trackId;
            DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? CategoryName : displayLabel.Trim();
        }

        public string CategoryName { get; }
        public Guid? TrackId { get; }
        public string DisplayLabel { get; }
    }
}
