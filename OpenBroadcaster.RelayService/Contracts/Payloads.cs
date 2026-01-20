using System.Text.Json.Serialization;

namespace OpenBroadcaster.RelayService.Contracts.Payloads
{
    // ==========================================================================
    // AUTHENTICATION PAYLOADS
    // ==========================================================================

    public sealed class AuthenticatePayload
    {
        [JsonPropertyName("stationId")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("stationToken")]
        public string StationToken { get; set; } = string.Empty;

        [JsonPropertyName("stationName")]
        public string? StationName { get; set; }

        [JsonPropertyName("clientVersion")]
        public string? ClientVersion { get; set; }
    }

    public sealed class AuthResultPayload
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("sessionToken")]
        public string? SessionToken { get; set; }

        [JsonPropertyName("serverTime")]
        public DateTimeOffset? ServerTime { get; set; }
    }

    // ==========================================================================
    // SYSTEM PAYLOADS
    // ==========================================================================

    public sealed class HeartbeatPayload
    {
        [JsonPropertyName("sentAt")]
        public long SentAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public sealed class ErrorPayload
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        public string? Details { get; set; }

        [JsonPropertyName("sourceType")]
        public string? SourceType { get; set; }
    }

    public static class ErrorCodes
    {
        public const string AuthRequired = "ERR_AUTH_REQUIRED";
        public const string AuthFailed = "ERR_AUTH_FAILED";
        public const string AuthExpired = "ERR_AUTH_EXPIRED";
        public const string PermissionDenied = "ERR_PERMISSION_DENIED";
        public const string InvalidPayload = "ERR_INVALID_PAYLOAD";
        public const string MissingField = "ERR_MISSING_FIELD";
        public const string InvalidFormat = "ERR_INVALID_FORMAT";
        public const string StationNotFound = "ERR_STATION_NOT_FOUND";
        public const string StationOffline = "ERR_STATION_OFFLINE";
        public const string TrackNotFound = "ERR_TRACK_NOT_FOUND";
        public const string QueueFull = "ERR_QUEUE_FULL";
        public const string OperationFailed = "ERR_OPERATION_FAILED";
        public const string Timeout = "ERR_TIMEOUT";
        public const string RateLimited = "ERR_RATE_LIMITED";
        public const string InternalError = "ERR_INTERNAL";
        public const string NotImplemented = "ERR_NOT_IMPLEMENTED";
    }

    // ==========================================================================
    // NOW PLAYING PAYLOADS
    // ==========================================================================

    public sealed class NowPlayingPayload
    {
        [JsonPropertyName("isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("album")]
        public string? Album { get; set; }

        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        [JsonPropertyName("positionSeconds")]
        public double PositionSeconds { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("endsAt")]
        public DateTimeOffset? EndsAt { get; set; }

        [JsonPropertyName("artworkUrl")]
        public string? ArtworkUrl { get; set; }

        [JsonPropertyName("trackId")]
        public string? TrackId { get; set; }

        [JsonPropertyName("requestedBy")]
        public string? RequestedBy { get; set; }

        [JsonPropertyName("showName")]
        public string? ShowName { get; set; }

        [JsonPropertyName("djName")]
        public string? DjName { get; set; }

        public static NowPlayingPayload Empty() => new()
        {
            IsPlaying = false,
            Title = string.Empty,
            Artist = string.Empty
        };
    }

    // ==========================================================================
    // QUEUE PAYLOADS
    // ==========================================================================

    public sealed class QueueStatePayload
    {
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("totalDurationSeconds")]
        public double TotalDurationSeconds { get; set; }

        [JsonPropertyName("items")]
        public List<QueueItemDto> Items { get; set; } = new();

        [JsonPropertyName("isTruncated")]
        public bool IsTruncated { get; set; }

        [JsonPropertyName("maxQueueSize")]
        public int MaxQueueSize { get; set; } = 100;
    }

    public sealed class QueueItemDto
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("requestedBy")]
        public string? RequestedBy { get; set; }

        [JsonPropertyName("addedAt")]
        public DateTimeOffset AddedAt { get; set; }

        [JsonPropertyName("estimatedPlayTime")]
        public DateTimeOffset? EstimatedPlayTime { get; set; }
    }

    public sealed class QueueAddPayload
    {
        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public int Position { get; set; } = -1;

        [JsonPropertyName("requestedBy")]
        public string? RequestedBy { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "Remote";
    }

    public sealed class QueueAddResultPayload
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }
    }

    public sealed class QueueRemovePayload
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }
    }

    // ==========================================================================
    // LIBRARY PAYLOADS
    // ==========================================================================

    public sealed class LibrarySearchPayload
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        [JsonPropertyName("album")]
        public string? Album { get; set; }

        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        [JsonPropertyName("yearFrom")]
        public int? YearFrom { get; set; }

        [JsonPropertyName("yearTo")]
        public int? YearTo { get; set; }

        [JsonPropertyName("categoryIds")]
        public List<string>? CategoryIds { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 50;

        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        [JsonPropertyName("sortBy")]
        public string SortBy { get; set; } = "relevance";

        [JsonPropertyName("sortDirection")]
        public string SortDirection { get; set; } = "asc";
    }

    public sealed class LibrarySearchResultPayload
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("tracks")]
        public List<LibraryTrackDto> Tracks { get; set; } = new();
    }

    public sealed class LibraryTrackDto
    {
        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("album")]
        public string Album { get; set; } = string.Empty;

        [JsonPropertyName("genre")]
        public string Genre { get; set; } = string.Empty;

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        [JsonPropertyName("isRequestable")]
        public bool IsRequestable { get; set; } = true;

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();

        [JsonPropertyName("artworkUrl")]
        public string? ArtworkUrl { get; set; }
    }

    // ==========================================================================
    // SONG REQUEST PAYLOADS
    // ==========================================================================

    public sealed class SongRequestPayload
    {
        [JsonPropertyName("trackId")]
        public string TrackId { get; set; } = string.Empty;

        [JsonPropertyName("requesterName")]
        public string RequesterName { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        [JsonPropertyName("requestedAt")]
        public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class SongRequestResultPayload
    {
        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        [JsonPropertyName("queuePosition")]
        public int? QueuePosition { get; set; }

        [JsonPropertyName("estimatedWaitMinutes")]
        public int? EstimatedWaitMinutes { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("rejectionReason")]
        public string? RejectionReason { get; set; }
    }
}
