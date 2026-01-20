using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts.Payloads
{
    /// <summary>
    /// Represents the current state of the queue.
    /// </summary>
    public sealed class QueueStatePayload
    {
        /// <summary>
        /// Total number of items in the queue.
        /// </summary>
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        /// <summary>
        /// Total duration of all queued items in seconds.
        /// </summary>
        [JsonPropertyName("totalDurationSeconds")]
        public double TotalDurationSeconds { get; set; }

        /// <summary>
        /// The queued tracks (may be truncated for large queues).
        /// </summary>
        [JsonPropertyName("items")]
        public List<QueueItemDto> Items { get; set; } = new();

        /// <summary>
        /// True if the items list is truncated (large queue).
        /// </summary>
        [JsonPropertyName("isTruncated")]
        public bool IsTruncated { get; set; }

        /// <summary>
        /// Maximum number of items that can be queued.
        /// </summary>
        [JsonPropertyName("maxQueueSize")]
        public int MaxQueueSize { get; set; } = 100;
    }

    /// <summary>
    /// A single item in the queue for wire transfer.
    /// </summary>
    public sealed class QueueItemDto
    {
        /// <summary>
        /// Position in queue (0-based index).
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        /// <summary>
        /// Internal track ID.
        /// </summary>
        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        /// <summary>
        /// Track title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Artist name.
        /// </summary>
        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// How this item was added to the queue.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// If requested, who requested it.
        /// </summary>
        [JsonPropertyName("requestedBy")]
        public string? RequestedBy { get; set; }

        /// <summary>
        /// When this item was added to the queue.
        /// </summary>
        [JsonPropertyName("addedAt")]
        public DateTimeOffset AddedAt { get; set; }

        /// <summary>
        /// Estimated time when this track will play.
        /// </summary>
        [JsonPropertyName("estimatedPlayTime")]
        public DateTimeOffset? EstimatedPlayTime { get; set; }
    }

    /// <summary>
    /// Request to add a track to the queue.
    /// </summary>
    public sealed class QueueAddPayload
    {
        /// <summary>
        /// The track ID to add (from library search results).
        /// </summary>
        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        /// <summary>
        /// Position to insert at (-1 for end of queue).
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; } = -1;

        /// <summary>
        /// Who is making this request (for attribution).
        /// </summary>
        [JsonPropertyName("requestedBy")]
        public string? RequestedBy { get; set; }

        /// <summary>
        /// Source label for the queue entry.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = "Remote";
    }

    /// <summary>
    /// Result of a queue add operation.
    /// </summary>
    public sealed class QueueAddResultPayload
    {
        /// <summary>
        /// True if the track was successfully added.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// The position where the track was inserted.
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        /// <summary>
        /// Human-readable result message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Error code if failed.
        /// </summary>
        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Request to remove a track from the queue.
    /// </summary>
    public sealed class QueueRemovePayload
    {
        /// <summary>
        /// Position of the item to remove (0-based).
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }
    }
}
