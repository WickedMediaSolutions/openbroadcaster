using System;
using System.Collections.Generic;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Overlay;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class OverlaySnapshotFactoryTests
    {
        [Fact]
        public void CreateSnapshot_UsesPlayingDeckForNowPlaying()
        {
            var factory = new OverlaySnapshotFactory();
            factory.UpdateSettings(new OverlaySettings());

            var playingItem = new QueueItem(
                new Track("City Nights", "Analog Youth", "Neon Season", "Indie", 2024, TimeSpan.FromMinutes(3)),
                QueueSource.Manual,
                "Studio",
                string.Empty);

            var deckState = new OverlayDeckState(
                DeckIdentifier.A,
                playingItem,
                true,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(90));

            var snapshot = factory.Create(new[] { deckState }, new List<QueueItem> { playingItem }, Array.Empty<QueueItem>());

            Assert.NotNull(snapshot.NowPlaying);
            Assert.Equal("City Nights", snapshot.NowPlaying!.Title);
            Assert.Equal(90, snapshot.NowPlaying.ElapsedSeconds);
            Assert.True(snapshot.NowPlaying.IsPlaying);
        }

        [Fact]
        public void CreateSnapshot_FiltersRequestsAndHistory()
        {
            var factory = new OverlaySnapshotFactory();
            factory.UpdateSettings(new OverlaySettings
            {
                RequestListLimit = 1,
                RecentListLimit = 2
            });

            var request = new QueueItem(
                new Track("Request Song", "Listener", "Channel", "Alt", 2023, TimeSpan.FromMinutes(4)),
                QueueSource.Twitch,
                "Twitch",
                "djviewer");

            var otherQueue = new QueueItem(
                new Track("Auto", "DJ", "Show", "Alt", 2023, TimeSpan.FromMinutes(4)),
                QueueSource.AutoDj,
                "AutoDJ",
                string.Empty);

            var history = new List<QueueItem>
            {
                new QueueItem(new Track("History 1", "Artist", "Record", "Alt", 2022, TimeSpan.FromMinutes(3)), QueueSource.Manual, "Studio", string.Empty),
                new QueueItem(new Track("History 2", "Artist", "Record", "Alt", 2022, TimeSpan.FromMinutes(3)), QueueSource.Manual, "Studio", string.Empty),
                new QueueItem(new Track("History 3", "Artist", "Record", "Alt", 2022, TimeSpan.FromMinutes(3)), QueueSource.Manual, "Studio", string.Empty)
            };

            var snapshot = factory.Create(Array.Empty<OverlayDeckState>(), new List<QueueItem> { request, otherQueue }, history);

            Assert.Null(snapshot.NowPlaying);
            Assert.Single(snapshot.Requests);
            Assert.Equal("Request Song", snapshot.Requests[0].Title);
            Assert.Equal("djviewer", snapshot.Requests[0].RequestedBy);
            Assert.Equal(2, snapshot.Recent.Count);
            Assert.Equal("History 1", snapshot.Recent[0].Title);
        }
    }
}
