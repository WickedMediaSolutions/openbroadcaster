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

        public bool HasRequester => !string.IsNullOrWhiteSpace(Underlying.RequestedBy);
        public bool IsTwitchRequest => Underlying.SourceType == QueueSource.Twitch && HasRequester;
        public bool IsWebRequest => Underlying.SourceType == QueueSource.WebRequest && HasRequester;

        public string RequestedByDisplay => HasRequester ? $"Requested by {Underlying.RequestedBy}" : string.Empty;

        public string WebRequestDisplay
        {
            get
            {
                if (!IsWebRequest)
                {
                    return string.Empty;
                }

                var baseText = RequestedByDisplay;
                if (!string.IsNullOrWhiteSpace(Underlying.RequestMessage))
                {
                    return $"{baseText} — {Underlying.RequestMessage}";
                }

                return baseText;
            }
        }

        // Keep a reference to the core model for commands
        public QueueItem Underlying { get; }
    }
}
