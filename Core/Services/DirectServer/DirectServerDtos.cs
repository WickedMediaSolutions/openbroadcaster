using System;
using System.Collections.Generic;

namespace OpenBroadcaster.Core.Services.DirectServer;

/// <summary>
/// Data transfer objects for the Direct Server API responses.
/// </summary>
public static class DirectServerDtos
{
    /// <summary>
    /// Now playing track information.
    /// </summary>
    public class NowPlayingResponse
    {
        public string? TrackId { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? ArtworkUrl { get; set; }
        public int Duration { get; set; }
        public int Position { get; set; }
        public string? RequestedBy { get; set; }
        public string? Type { get; set; }
        public bool IsPlaying { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Queue response with list of upcoming tracks.
    /// </summary>
    public class QueueResponse
    {
        public List<QueueItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Individual queue item.
    /// </summary>
    public class QueueItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? ArtworkUrl { get; set; }
        public int Duration { get; set; }
        public string? RequestedBy { get; set; }
        public string? Type { get; set; }
    }

    /// <summary>
    /// Library search response.
    /// </summary>
    public class LibrarySearchResponse
    {
        public List<LibraryItem> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PerPage { get; set; } = 20;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Library item.
    /// </summary>
    public class LibraryItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? ArtworkUrl { get; set; }
        public int Duration { get; set; }
        public string? Type { get; set; }
        public string? FilePath { get; set; }
    }

    /// <summary>
    /// Song request submission.
    /// </summary>
    public class SongRequestSubmission
    {
        public string? TrackId { get; set; }
        public string? RequesterName { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// Song request response.
    /// </summary>
    public class SongRequestResponse
    {
        public bool Success { get; set; }
        public string? RequestId { get; set; }
        public string? Message { get; set; }
        public int? QueuePosition { get; set; }
    }

    /// <summary>
    /// Server status response.
    /// </summary>
    public class StatusResponse
    {
        public string Status { get; set; } = "online";
        public string Version { get; set; } = "1.3.0";
        public string StationName { get; set; } = "OpenBroadcaster";
        public bool RequestsEnabled { get; set; } = true;
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Error response.
    /// </summary>
    public class ErrorResponse
    {
        public bool Success { get; set; } = false;
        public string? Error { get; set; }
        public int Code { get; set; }
    }
}
