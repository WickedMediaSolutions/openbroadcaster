using System.Linq;
using OpenBroadcaster.Core.Automation;
using Xunit;

namespace OpenBroadcaster.Tests.Automation
{
    public sealed class ClockwheelSchedulerTests
    {
        [Fact]
        public void NextSlot_CyclesThroughConfiguredSlots()
        {
            var scheduler = new ClockwheelScheduler();
            scheduler.LoadSlots(new[]
            {
                new ClockwheelSlot("Hot AC"),
                new ClockwheelSlot("Imaging")
            });

            var first = scheduler.NextSlot();
            var second = scheduler.NextSlot();
            var third = scheduler.NextSlot();

            Assert.Equal("Hot AC", first?.CategoryName);
            Assert.Equal("Imaging", second?.CategoryName);
            Assert.Equal("Hot AC", third?.CategoryName);
        }

        [Fact]
        public void GetUpcoming_ReturnsWindowFromCurrentPointer()
        {
            var scheduler = new ClockwheelScheduler();
            scheduler.LoadSlots(new[]
            {
                new ClockwheelSlot("Hot AC"),
                new ClockwheelSlot("Imaging"),
                new ClockwheelSlot("Night")
            });

            _ = scheduler.NextSlot(); // advance pointer once
            var upcoming = scheduler.GetUpcoming(4).Select(slot => slot.CategoryName).ToArray();

            Assert.Equal(new[] { "Imaging", "Night", "Hot AC", "Imaging" }, upcoming);
        }

        [Fact]
        public void NextSlot_ReturnsNullWhenEmpty()
        {
            var scheduler = new ClockwheelScheduler();
            Assert.Null(scheduler.NextSlot());
            Assert.Empty(scheduler.GetUpcoming(3));
        }
    }
}
