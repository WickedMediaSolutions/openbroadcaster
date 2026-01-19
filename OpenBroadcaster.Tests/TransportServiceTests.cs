using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class TransportServiceTests
    {
        [Fact]
        public void Load_PublishesDeckStateWithMetadata()
        {
            var eventBus = new EventBus();
            var queueService = new QueueService();
            using var transport = new TransportService(eventBus, queueService);
            var events = new List<DeckStateChangedEvent>();
            using var subscription = eventBus.Subscribe<DeckStateChangedEvent>(events.Add);

            var item = BuildQueueItem("Track A");
            queueService.Enqueue(item);
            var loaded = transport.RequestNextFromQueue(DeckIdentifier.A);
            Assert.NotNull(loaded);

            Assert.NotEmpty(events);
            var latest = events[^1];
            Assert.Equal(DeckIdentifier.A, latest.DeckId);
            Assert.Equal("Track A", latest.QueueItem?.Track.Title);
            Assert.False(latest.IsPlaying);
        }

        [Fact]
        public void Play_TogglesIsPlayingFlag()
        {
            var eventBus = new EventBus();
            var queueService = new QueueService();
            using var transport = new TransportService(eventBus, queueService);
            var events = new List<DeckStateChangedEvent>();
            using var subscription = eventBus.Subscribe<DeckStateChangedEvent>(events.Add);

            var item = BuildQueueItem("Track B");
            queueService.Enqueue(item);
            transport.RequestNextFromQueue(DeckIdentifier.B);
            events.Clear();

            transport.Play(DeckIdentifier.B);

            Assert.NotEmpty(events);
            var latest = events[^1];
            Assert.True(latest.IsPlaying);
            Assert.Equal(DeckIdentifier.B, latest.DeckId);
        }

        [Fact]
        public async Task Play_PublishesPeriodicElapsedUpdates()
        {
            var eventBus = new EventBus();
            var queueService = new QueueService();
            using var transport = new TransportService(eventBus, queueService, telemetryInterval: TimeSpan.FromMilliseconds(25));
            var events = new List<DeckStateChangedEvent>();
            using var subscription = eventBus.Subscribe<DeckStateChangedEvent>(events.Add);

            var item = BuildQueueItem("Track C");
            queueService.Enqueue(item);
            transport.RequestNextFromQueue(DeckIdentifier.A);
            events.Clear();

            transport.Play(DeckIdentifier.A);
            await Task.Delay(150);

            var telemetryEvent = events.FindLast(evt => evt.Elapsed > TimeSpan.Zero);
            Assert.NotNull(telemetryEvent);
            Assert.Equal(DeckIdentifier.A, telemetryEvent!.DeckId);
            Assert.True(telemetryEvent.Elapsed > TimeSpan.Zero);
            Assert.True(telemetryEvent.Remaining < item.Track.Duration);

            transport.Stop(DeckIdentifier.A);
        }

        private static QueueItem BuildQueueItem(string title)
        {
            var track = new Track(title, "Unit Artist", "Unit Album", "Unit", 2024, System.TimeSpan.FromMinutes(3));
            return new QueueItem(track, QueueSource.Manual, "UnitTest", "Tester");
        }
    }
}
