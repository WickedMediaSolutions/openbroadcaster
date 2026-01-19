using System;
using System.Collections.Generic;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Requests;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class RequestPolicyEvaluatorTests
    {
        [Fact]
        public void TryValidate_FailsWhenQueueFull()
        {
            var evaluator = new RequestPolicyEvaluator();
            var settings = new RequestSettings
            {
                MaxPendingRequests = 1,
                MaxRequestsPerUser = 5
            };

            var snapshot = new List<QueueItem>
            {
                BuildRequestedItem("userA")
            };

            var allowed = evaluator.TryValidate(settings, snapshot, "userB", out var reason);
            Assert.False(allowed);
            Assert.Contains("queue full", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryValidate_FailsWhenUserHasTooManyRequests()
        {
            var evaluator = new RequestPolicyEvaluator();
            var settings = new RequestSettings
            {
                MaxPendingRequests = 5,
                MaxRequestsPerUser = 1
            };

            var snapshot = new List<QueueItem>
            {
                BuildRequestedItem("djviewer")
            };

            var allowed = evaluator.TryValidate(settings, snapshot, "djviewer", out var reason);
            Assert.False(allowed);
            Assert.Contains("pending requests", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryValidate_AllowsWhenUnderLimits()
        {
            var evaluator = new RequestPolicyEvaluator();
            var settings = new RequestSettings
            {
                MaxPendingRequests = 5,
                MaxRequestsPerUser = 2
            };

            var snapshot = new List<QueueItem>
            {
                BuildRequestedItem("viewer1"),
                BuildRequestedItem("viewer2")
            };

            var allowed = evaluator.TryValidate(settings, snapshot, "viewer1", out var reason);
            Assert.True(allowed);
            Assert.Equal(string.Empty, reason);
        }

        private static QueueItem BuildRequestedItem(string requestedBy)
        {
            var track = new Track("Test", "Unit", "Suite", "Alt", 2024, TimeSpan.FromMinutes(3));
            return new QueueItem(track, QueueSource.Twitch, "Requests", requestedBy);
        }
    }
}
