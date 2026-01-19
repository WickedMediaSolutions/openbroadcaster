using System;
using System.Collections.Generic;
using System.IO;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class AutoDjControllerTests : IDisposable
    {
        private readonly string _libraryPath;
        private readonly LibraryService _libraryService;

        public AutoDjControllerTests()
        {
            _libraryPath = Path.Combine(Path.GetTempPath(), $"library-tests-{Guid.NewGuid():N}.json");
            _libraryService = new LibraryService(_libraryPath);
        }

        [Fact]
        public void Enable_FillsQueueToTargetDepth()
        {
            var queue = new QueueService();
            var rotation = BuildRotationEngine(out var scheduler);

            using var controller = new AutoDjController(queue, rotation, scheduler, _libraryService);
            controller.TargetQueueDepth = 3;
            controller.Enable();

            var snapshot = queue.Snapshot();
            Assert.Equal(3, snapshot.Count);
            foreach (var item in snapshot)
            {
                Assert.Equal(QueueSource.AutoDj, item.SourceType);
            }
        }

        [Fact]
        public void Enable_WhenClockwheelEmpty_ReportsStatusAndSkipsEnqueue()
        {
            var queue = new QueueService();
            var rotation = new RotationEngine();
            var scheduler = new ClockwheelScheduler();
            string? lastStatus = null;

            using var controller = new AutoDjController(queue, rotation, scheduler, _libraryService);
            controller.TargetQueueDepth = 1;
            controller.StatusChanged += (_, message) => lastStatus = message;
            controller.Enable();

            Assert.Empty(queue.Snapshot());
            Assert.False(string.IsNullOrWhiteSpace(lastStatus));
            Assert.Contains("Clockwheel", lastStatus ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private RotationEngine BuildRotationEngine(out ClockwheelScheduler scheduler)
        {
            var tracks = new List<Track>
            {
                _libraryService.SaveTrack(new Track("Satellite", "WinAmp Society", "Rotation", "Hot AC", 2024, TimeSpan.FromMinutes(3))),
                _libraryService.SaveTrack(new Track("Night Pulse", "Deck Ninety", "Rotation", "Hot AC", 2024, TimeSpan.FromMinutes(4)))
            };

            var rotation = new RotationEngine();
            rotation.LoadCategories(new[] { new RotationCategoryDefinition("Hot AC", tracks) });

            scheduler = new ClockwheelScheduler();
            scheduler.LoadSlots(new[]
            {
                new ClockwheelSlot("Hot AC"),
                new ClockwheelSlot("Hot AC")
            });

            return rotation;
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_libraryPath))
                {
                    File.Delete(_libraryPath);
                }
            }
            catch
            {
            }
        }
    }
}
