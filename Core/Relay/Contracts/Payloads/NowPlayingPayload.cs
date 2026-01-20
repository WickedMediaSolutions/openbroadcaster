using System;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts.Payloads
{
    /// <summary>
    /// Now playing information broadcast from the desktop app.
    /// 
    /// DESIGN RATIONALE:
    /// - Includes all metadata commonly needed by web widgets
    /// - Timing information enables progress bars and countdowns
    /// - Album art URL allows for external image hosting
    /// - History tracking via previous track info
    /// </summary>
    public sealed class NowPlayingPayload
    {
        /// <summary>
        /// True if something is currently playing, false if stopped/idle.
        /// </summary>
        [JsonPropertyName("isPlaying")]
        public bool IsPlaying { get; set; }

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
        /// Album name (optional).
        /// </summary>
        [JsonPropertyName("album")]
        public string? Album { get; set; }

        /// <summary>
        /// Genre (optional).
        /// </summary>
        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        /// <summary>
        /// Release year (optional, 0 if unknown).
        /// </summary>
        [JsonPropertyName("year")]
        public int Year { get; set; }

        /// <summary>
        /// Track duration in seconds.
        /// </summary>
        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Current playback position in seconds.
        /// </summary>
        [JsonPropertyName("positionSeconds")]
        public double PositionSeconds { get; set; }

        /// <summary>
        /// UTC timestamp when this track started playing.
        /// Enables accurate progress calculation on clients.
        /// </summary>
        [JsonPropertyName("startedAt")]
        public DateTimeOffset? StartedAt { get; set; }

        /// <summary>
        /// Estimated UTC timestamp when this track will end.
        /// </summary>
        [JsonPropertyName("endsAt")]
        public DateTimeOffset? EndsAt { get; set; }

        /// <summary>
        /// URL to album artwork (optional).
        /// Can be a CDN URL, station website URL, or data URI.
        /// </summary>
        [JsonPropertyName("artworkUrl")]
        public string? ArtworkUrl { get; set; }

        /// <summary>
        /// Internal track ID in the library (optional).
        /// Useful for request systems that reference specific tracks.
        /// </summary>
        [JsonPropertyName("trackId")]
        public string? TrackId { get; set; }

        /// <summary>
        /// If this track was requested, the requester's name.
        /// </summary>
        [JsonPropertyName("requestedBy")]
        public string? RequestedBy { get; set; }

        /// <summary>
        /// Show/program name if applicable.
        /// </summary>
        [JsonPropertyName("showName")]
        public string? ShowName { get; set; }

        /// <summary>
        /// DJ/host name if applicable.
        /// </summary>
        [JsonPropertyName("djName")]
        public string? DjName { get; set; }

        /// <summary>
        /// Source type for the current track (e.g., "AutoDj", "Manual", etc.).
        /// </summary>
        [JsonPropertyName("sourceType")]
        public string? SourceType { get; set; }

        /// <summary>
        /// Creates an empty "not playing" payload.
        /// </summary>
        public static NowPlayingPayload Empty() => new()
        {
            IsPlaying = false,
            Title = string.Empty,
            Artist = string.Empty
        };
    }
}
