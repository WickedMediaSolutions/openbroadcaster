using System;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts.Payloads
{
    /// <summary>
    /// Song request from a listener (via website).
    /// </summary>
    public sealed class SongRequestPayload
    {
        /// <summary>
        /// The track ID being requested (from library search).
        /// </summary>
        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        /// <summary>
        /// Requester's name/nickname.
        /// </summary>
        [JsonPropertyName("requesterName")]
        public string RequesterName { get; set; } = string.Empty;

        /// <summary>
        /// Optional message from the requester (dedication, etc.).
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Requester's email (for notifications, optional).
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>
        /// IP address of the requester (for rate limiting).
        /// Set by the relay, not the client.
        /// </summary>
        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        /// <summary>
        /// Timestamp of the request.
        /// </summary>
        [JsonPropertyName("requestedAt")]
        public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Result of a song request.
    /// </summary>
    public sealed class SongRequestResultPayload
    {
        /// <summary>
        /// True if the request was accepted.
        /// </summary>
        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        /// <summary>
        /// Position in queue if accepted.
        /// </summary>
        [JsonPropertyName("queuePosition")]
        public int? QueuePosition { get; set; }

        /// <summary>
        /// Estimated wait time in minutes.
        /// </summary>
        [JsonPropertyName("estimatedWaitMinutes")]
        public int? EstimatedWaitMinutes { get; set; }

        /// <summary>
        /// Human-readable result message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Rejection reason code if not accepted.
        /// </summary>
        [JsonPropertyName("rejectionReason")]
        public string? RejectionReason { get; set; }
    }

    /// <summary>
    /// Well-known rejection reason codes.
    /// </summary>
    public static class RequestRejectionReasons
    {
        /// <summary>Track was played recently.</summary>
        public const string RecentlyPlayed = "RECENTLY_PLAYED";

        /// <summary>Track is already in the queue.</summary>
        public const string AlreadyQueued = "ALREADY_QUEUED";

        /// <summary>Queue is full.</summary>
        public const string QueueFull = "QUEUE_FULL";

        /// <summary>Track is not requestable.</summary>
        public const string NotRequestable = "NOT_REQUESTABLE";

        /// <summary>Requester has exceeded rate limit.</summary>
        public const string RateLimited = "RATE_LIMITED";

        /// <summary>Requests are currently disabled.</summary>
        public const string RequestsDisabled = "REQUESTS_DISABLED";

        /// <summary>Track not found in library.</summary>
        public const string TrackNotFound = "TRACK_NOT_FOUND";

        /// <summary>Request validation failed.</summary>
        public const string ValidationFailed = "VALIDATION_FAILED";
    }
}
