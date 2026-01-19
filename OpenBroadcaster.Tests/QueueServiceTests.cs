using System;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class QueueServiceTests
    {
        [Fact]
        public void EnqueueAndDequeue_OperatesInFifoOrder()
        {
            var service = new QueueService();
            var first = BuildItem("First");
            var second = BuildItem("Second");

            service.Enqueue(first);
            service.Enqueue(second);

            var snapshot = service.Snapshot();
            Assert.Equal(2, snapshot.Count);
            Assert.Equal("First", snapshot[0].Track.Title);
            Assert.Equal("Second", snapshot[1].Track.Title);

            var fromQueue = service.Dequeue();
            Assert.NotNull(fromQueue);
            Assert.Equal("First", fromQueue!.Track.Title);

            var next = service.Dequeue();
            Assert.NotNull(next);
            Assert.Equal("Second", next!.Track.Title);
        }

        [Fact]
        public void Dequeue_ReturnsNullWhenEmpty()
        {
            var service = new QueueService();
            var result = service.Dequeue();
            Assert.Null(result);
        }

        [Fact]
        public void Reorder_MovesItemToTargetIndex()
        {
            var service = new QueueService();
            service.Enqueue(BuildItem("One"));
            service.Enqueue(BuildItem("Two"));
            service.Enqueue(BuildItem("Three"));

            var moved = service.Reorder(2, 0);
            Assert.True(moved);

            var snapshot = service.Snapshot();
            Assert.Equal("Three", snapshot[0].Track.Title);
            Assert.Equal("One", snapshot[1].Track.Title);
            Assert.Equal("Two", snapshot[2].Track.Title);
        }

        [Fact]
        public void HistorySnapshot_CapturesLastFiveDequeued()
        {
            var service = new QueueService();
            for (var i = 1; i <= 6; i++)
            {
                service.Enqueue(BuildItem($"Track {i}"));
            }

            for (var i = 0; i < 6; i++)
            {
                Assert.NotNull(service.Dequeue());
            }

            var history = service.HistorySnapshot();
            Assert.Equal(5, history.Count);
            Assert.Equal("Track 6", history[0].Track.Title);
            Assert.Equal("Track 2", history[history.Count - 1].Track.Title);
        }

        [Fact]
        public void QueueAndHistoryEvents_RaiseOnMutations()
        {
            var service = new QueueService();
            var queueChanged = 0;
            var historyChanged = 0;
            service.QueueChanged += (_, _) => queueChanged++;
            service.HistoryChanged += (_, _) => historyChanged++;

            service.Enqueue(BuildItem("One"));
            service.InsertAt(0, BuildItem("Two"));
            Assert.True(service.RemoveAt(1));
            Assert.NotNull(service.Dequeue());

            Assert.Equal(4, queueChanged);
            Assert.Equal(1, historyChanged);
        }

        [Fact]
        public void UpdateHistoryLimit_TrimsExistingEntries()
        {
            var service = new QueueService();
            for (var i = 1; i <= 6; i++)
            {
                service.Enqueue(BuildItem($"Song {i}"));
                service.Dequeue();
            }

            service.UpdateHistoryLimit(2);
            var history = service.HistorySnapshot();
            Assert.Equal(2, history.Count);
            Assert.Equal("Song 6", history[0].Track.Title);
            Assert.Equal("Song 5", history[1].Track.Title);
        }

        private static QueueItem BuildItem(string title)
        {
            var track = new Track(title, "Test Artist", "Compilation", "Test", 2025, TimeSpan.FromMinutes(3));
            return new QueueItem(track, QueueSource.Manual, "UnitTest", "Tester");
        }
    }
}
