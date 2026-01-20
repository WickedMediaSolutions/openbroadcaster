using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts.Payloads
{
    /// <summary>
    /// Library search request payload.
    /// </summary>
    public sealed class LibrarySearchPayload
    {
        /// <summary>
        /// Search query string.
        /// Searches across title, artist, and album.
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Filter by artist name (exact or partial match).
        /// </summary>
        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        /// <summary>
        /// Optional: Filter by album name.
        /// </summary>
        [JsonPropertyName("album")]
        public string? Album { get; set; }

        /// <summary>
        /// Optional: Filter by genre.
        /// </summary>
        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        /// <summary>
        /// Optional: Filter by year range (minimum).
        /// </summary>
        [JsonPropertyName("yearFrom")]
        public int? YearFrom { get; set; }

        /// <summary>
        /// Optional: Filter by year range (maximum).
        /// </summary>
        [JsonPropertyName("yearTo")]
        public int? YearTo { get; set; }

        /// <summary>
        /// Optional: Filter by category/rotation IDs.
        /// </summary>
        [JsonPropertyName("categoryIds")]
        public List<string>? CategoryIds { get; set; }

        /// <summary>
        /// Maximum number of results to return (default: 50, max: 200).
        /// </summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 50;

        /// <summary>
        /// Result offset for pagination.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Sort field: "title", "artist", "album", "duration", "year", "relevance".
        /// </summary>
        [JsonPropertyName("sortBy")]
        public string SortBy { get; set; } = "relevance";

        /// <summary>
        /// Sort direction: "asc" or "desc".
        /// </summary>
        [JsonPropertyName("sortDirection")]
        public string SortDirection { get; set; } = "asc";
    }

    /// <summary>
    /// Library search result payload.
    /// </summary>
    public sealed class LibrarySearchResultPayload
    {
        /// <summary>
        /// The search query that produced these results.
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Total number of matching tracks.
        /// </summary>
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page offset.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        /// <summary>
        /// Number of results in this response.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// The matching tracks.
        /// </summary>
        [JsonPropertyName("tracks")]
        public List<LibraryTrackDto> Tracks { get; set; } = new();
    }

    /// <summary>
    /// A track from the library for wire transfer.
    /// </summary>
    public sealed class LibraryTrackDto
    {
        /// <summary>
        /// Internal track ID (use this for queue.add requests).
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
        /// Album name.
        /// </summary>
        [JsonPropertyName("album")]
        public string Album { get; set; } = string.Empty;

        /// <summary>
        /// Genre.
        /// </summary>
        [JsonPropertyName("genre")]
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// Release year.
        /// </summary>
        [JsonPropertyName("year")]
        public int Year { get; set; }

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Whether this track is requestable (enabled for requests).
        /// </summary>
        [JsonPropertyName("isRequestable")]
        public bool IsRequestable { get; set; } = true;

        /// <summary>
        /// Category/rotation names this track belongs to.
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// Album artwork URL if available.
        /// </summary>
        [JsonPropertyName("artworkUrl")]
        public string? ArtworkUrl { get; set; }
    }
}
