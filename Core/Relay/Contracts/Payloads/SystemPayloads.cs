using System;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts.Payloads
{
    /// <summary>
    /// Heartbeat payload for ping/pong messages.
    /// The payload is optional - an empty ping/pong is valid.
    /// </summary>
    public sealed class HeartbeatPayload
    {
        /// <summary>
        /// Unix timestamp (milliseconds) when the ping was sent.
        /// Used to calculate round-trip time.
        /// </summary>
        [JsonPropertyName("sentAt")]
        public long SentAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Error payload for failed operations.
    /// </summary>
    public sealed class ErrorPayload
    {
        /// <summary>
        /// Machine-readable error code.
        /// Use well-known codes from ErrorCodes class.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional context about the error (optional).
        /// May contain field names, constraint violations, etc.
        /// </summary>
        [JsonPropertyName("details")]
        public string? Details { get; set; }

        /// <summary>
        /// The message type that caused this error (optional).
        /// </summary>
        [JsonPropertyName("sourceType")]
        public string? SourceType { get; set; }
    }

    /// <summary>
    /// Well-known error codes for consistent error handling.
    /// </summary>
    public static class ErrorCodes
    {
        // Authentication errors (1xxx)
        public const string AuthRequired = "ERR_AUTH_REQUIRED";
        public const string AuthFailed = "ERR_AUTH_FAILED";
        public const string AuthExpired = "ERR_AUTH_EXPIRED";
        public const string PermissionDenied = "ERR_PERMISSION_DENIED";

        // Validation errors (2xxx)
        public const string InvalidPayload = "ERR_INVALID_PAYLOAD";
        public const string MissingField = "ERR_MISSING_FIELD";
        public const string InvalidFormat = "ERR_INVALID_FORMAT";

        // Resource errors (3xxx)
        public const string StationNotFound = "ERR_STATION_NOT_FOUND";
        public const string StationOffline = "ERR_STATION_OFFLINE";
        public const string TrackNotFound = "ERR_TRACK_NOT_FOUND";
        public const string QueueFull = "ERR_QUEUE_FULL";

        // Operation errors (4xxx)
        public const string OperationFailed = "ERR_OPERATION_FAILED";
        public const string Timeout = "ERR_TIMEOUT";
        public const string RateLimited = "ERR_RATE_LIMITED";

        // Internal errors (5xxx)
        public const string InternalError = "ERR_INTERNAL";
        public const string NotImplemented = "ERR_NOT_IMPLEMENTED";
    }
}
