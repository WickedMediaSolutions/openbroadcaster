using System;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Tests.Infrastructure;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class TwitchIntegrationServiceTests
    {
        [Fact]
        public void SongRequestSelection_AddsQueueItemAndDebitsPoints()
        {
            using var host = new StudioTestHost();
            var settings = host.CreateDefaultTwitchSettings();
            settings.RequestCost = 5;
            host.TwitchService.UpdateSettings(settings);
            host.LoyaltyLedger.AddPoints("listener", 25);
            var track = host.AddLibraryTrack("Favorite Song", "Listener Artist");

            var context = new TwitchChatMessage("listener", "!s", DateTime.UtcNow);
            host.TwitchService.HandleCommand(context, "!s", "Favorite");
            host.TwitchService.HandleCommand(context, "!1", string.Empty);

            var snapshot = host.QueueService.Snapshot();
            Assert.Single(snapshot);
            Assert.Equal(track.Id, snapshot[0].Track.Id);
            Assert.Equal("listener", snapshot[0].RequestedBy);

            var remaining = host.LoyaltyLedger.GetPoints("listener");
            Assert.Equal(20, remaining);
        }

        [Fact]
        public void PlayNext_ReordersQueueWhenPointsAvailable()
        {
            using var host = new StudioTestHost();
            var settings = host.CreateDefaultTwitchSettings();
            settings.RequestCost = 0;
            settings.PlayNextCost = 10;
            host.TwitchService.UpdateSettings(settings);
            host.LoyaltyLedger.AddPoints("listener", 30);

            host.QueueService.Enqueue(new QueueItem(host.CreateTrack("Intro"), QueueSource.Manual, "Manual", "other"));
            host.QueueService.Enqueue(new QueueItem(host.CreateTrack("First Request"), QueueSource.Manual, "Manual", "listener"));
            host.QueueService.Enqueue(new QueueItem(host.CreateTrack("Second Request"), QueueSource.Manual, "Manual", "listener"));

            var context = new TwitchChatMessage("listener", "!playnext", DateTime.UtcNow);
            host.TwitchService.HandleCommand(context, "!playnext", string.Empty);

            var snapshot = host.QueueService.Snapshot();
            Assert.Equal("Second Request", snapshot[0].Track.Title);
            Assert.Equal("listener", snapshot[0].RequestedBy);

            var remaining = host.LoyaltyLedger.GetPoints("listener");
            Assert.Equal(20, remaining);
        }

        [Fact]
        public void PlayNextWithSelection_PrioritizesSearchResult()
        {
            using var host = new StudioTestHost();
            var settings = host.CreateDefaultTwitchSettings();
            settings.RequestCost = 0;
            settings.PlayNextCost = 15;
            host.TwitchService.UpdateSettings(settings);
            host.LoyaltyLedger.AddPoints("listener", 30);

            var manualTrack = host.CreateTrack("Manual Bed");
            host.QueueService.Enqueue(new QueueItem(manualTrack, QueueSource.Manual, "Manual", "dj"));
            var target = host.AddLibraryTrack("City Nights", "Listener Artist");

            var context = new TwitchChatMessage("listener", "!s", DateTime.UtcNow);
            host.TwitchService.HandleCommand(context, "!s", "City");
            host.TwitchService.HandleCommand(context, "!playnext", "1");

            var snapshot = host.QueueService.Snapshot();
            Assert.Equal(target.Id, snapshot[0].Track.Id);
            Assert.Equal("listener", snapshot[0].RequestedBy);

            var remaining = host.LoyaltyLedger.GetPoints("listener");
            Assert.Equal(15, remaining);
        }
    }
}
