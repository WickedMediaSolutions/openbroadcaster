using System;
using System.Diagnostics;

namespace OpenBroadcaster.Core.Models
{
    public sealed class Deck
    {
        private readonly Stopwatch _stopwatch = new();

        public Deck(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
        public QueueItem? CurrentQueueItem { get; private set; }
        public bool IsPlaying => _stopwatch.IsRunning;
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        public DeckStatus Status { get; private set; } = DeckStatus.Empty;
        public TimeSpan Remaining
        {
            get
            {
                if (CurrentQueueItem?.Track == null)
                {
                    return TimeSpan.Zero;
                }

                var remaining = CurrentQueueItem.Track.Duration - Elapsed;
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }

        public void Load(QueueItem queueItem)
        {
            CurrentQueueItem = queueItem ?? throw new ArgumentNullException(nameof(queueItem));
            Reset();
            Status = DeckStatus.Loaded;
        }

        public void Unload()
        {
            Stop();
            CurrentQueueItem = null;
            Status = DeckStatus.Empty;
        }

        public void Play()
        {
            if (CurrentQueueItem == null)
            {
                throw new InvalidOperationException("Cannot start playback without a loaded queue item.");
            }

            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                Status = DeckStatus.Playing;
            }
        }

        public void Pause()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
                Status = DeckStatus.Paused;
            }
        }

        public void Stop()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
            }

            _stopwatch.Reset();
            if (CurrentQueueItem != null)
            {
                Status = DeckStatus.Stopped;
            }
            else
            {
                Status = DeckStatus.Empty;
            }
        }

        public void Reset()
        {
            _stopwatch.Reset();
        }
    }
}