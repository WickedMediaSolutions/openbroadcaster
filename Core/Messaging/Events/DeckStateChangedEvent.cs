using System;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Messaging.Events
{
    public sealed class DeckStateChangedEvent
    {
        public DeckStateChangedEvent(DeckIdentifier deckId, QueueItem? queueItem, bool isPlaying, TimeSpan elapsed, TimeSpan remaining, DeckStatus status)
        {
            DeckId = deckId;
            QueueItem = queueItem;
            IsPlaying = isPlaying;
            Elapsed = elapsed;
            Remaining = remaining;
            Status = status;
        }

        public DeckIdentifier DeckId { get; }
        public QueueItem? QueueItem { get; }
        public bool IsPlaying { get; }
        public TimeSpan Elapsed { get; }
        public TimeSpan Remaining { get; }
        public DeckStatus Status { get; }
    }
}
