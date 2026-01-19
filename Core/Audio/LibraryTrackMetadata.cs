using System;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class LibraryTrackMetadata
    {
        public LibraryTrackMetadata(string title, string artist, string album, string genre, int year, TimeSpan duration)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
            Artist = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist.Trim();
            Album = album?.Trim() ?? string.Empty;
            Genre = genre?.Trim() ?? string.Empty;
            Year = year <= 0 ? DateTime.UtcNow.Year : year;
            Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        }

        public string Title { get; }
        public string Artist { get; }
        public string Album { get; }
        public string Genre { get; }
        public int Year { get; }
        public TimeSpan Duration { get; }
    }
}
