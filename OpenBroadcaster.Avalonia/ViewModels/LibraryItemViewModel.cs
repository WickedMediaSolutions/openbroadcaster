using System;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public sealed class LibraryItemViewModel
    {
        public LibraryItemViewModel(Track track)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            Id = track.Id;
            Title = track.Title;
            Artist = track.Artist;
            Album = track.Album;
            Duration = track.Duration.TotalHours >= 1
                ? track.Duration.ToString(@"h\:mm\:ss")
                : track.Duration.ToString(@"mm\:ss");
            Underlying = track;
        }

        public Guid Id { get; }
        public string Title { get; }
        public string Artist { get; }
        public string Album { get; }
        public string Duration { get; }

        public Track Underlying { get; }
    }
}
