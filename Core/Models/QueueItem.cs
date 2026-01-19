using System;

namespace OpenBroadcaster.Core.Models
{
    public sealed class QueueItem
    {
        public QueueItem(Track track, QueueSource sourceType, string sourceLabel, string requestedBy, string? rotationName = null, string? categoryName = null)
        {
            Track = track ?? throw new ArgumentNullException(nameof(track));
            SourceType = sourceType;
            Source = string.IsNullOrWhiteSpace(sourceLabel) ? sourceType.ToString() : sourceLabel.Trim();
            RequestedBy = requestedBy?.Trim() ?? string.Empty;
            RotationName = rotationName?.Trim() ?? string.Empty;
            CategoryName = categoryName?.Trim() ?? string.Empty;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public Track Track { get; }
        public QueueSource SourceType { get; }
        public string Source { get; }
        public string RequestedBy { get; }
        public string RotationName { get; }
        public string CategoryName { get; }
        public DateTime CreatedAtUtc { get; }

        public bool HasRequester => !string.IsNullOrWhiteSpace(RequestedBy);

        public string RequestAttribution => HasRequester
            ? $"Requested by {RequestedBy}"
            : string.Empty;
    }
}
