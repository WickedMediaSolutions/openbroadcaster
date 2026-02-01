using System;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public sealed class QueueItemViewModel
    {
        public QueueItemViewModel(QueueItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            Title = item.Track?.Title ?? string.Empty;
            Artist = item.Track?.Artist ?? string.Empty;
            Duration = item.Track == null
                ? string.Empty
                : item.Track.Duration.TotalHours >= 1
                    ? item.Track.Duration.ToString(@"h\:mm\:ss")
                    : item.Track.Duration.ToString(@"mm\:ss");
            Source = item.Source;
            RequestedBy = item.RequestedBy;
            Underlying = item;
        }

        public string Title { get; }
        public string Artist { get; }
        public string Duration { get; }
        public string Source { get; }
        public string RequestedBy { get; }

        // Keep a reference to the core model for commands
        public QueueItem Underlying { get; }
    }
}
