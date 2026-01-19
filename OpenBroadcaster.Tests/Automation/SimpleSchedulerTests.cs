using System;
using System.Collections.Generic;
using Moq;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests.Automation
{
    public class SimpleSchedulerTests
    {
        private readonly Mock<AutoDjSettingsService> _mockSettingsService;
        private readonly SimpleScheduler _scheduler;
        private readonly List<SimpleRotation> _rotations;
        private readonly List<SimpleScheduleRule> _rules;

        public SimpleSchedulerTests()
        {
            _mockSettingsService = new Mock<AutoDjSettingsService>(false);
            _rotations = new List<SimpleRotation>();
            _rules = new List<SimpleScheduleRule>();

            // Use reflection to set the private properties on the mock, since they have private setters.
            _mockSettingsService.SetupGet(s => s.Rotations).Returns(_rotations);
            _mockSettingsService.SetupGet(s => s.Rules).Returns(_rules);

            _scheduler = new SimpleScheduler(_mockSettingsService.Object);
        }

        [Fact]
        public void GetActiveRotationId_NoRules_ReturnsDefaultRotationId()
        {
            // Arrange
            var defaultRotationId = Guid.NewGuid();
            _mockSettingsService.SetupGet(s => s.DefaultRotationId).Returns(defaultRotationId);

            // Act
            var result = _scheduler.GetActiveRotationId();

            // Assert
            Assert.Equal(defaultRotationId, result);
        }

        [Fact]
        public void GetActiveRotationId_OneMatchingRule_ReturnsCorrectRotationId()
        {
            // Arrange
            var rotationId = Guid.NewGuid();
            _rules.Add(new SimpleScheduleRule
            {
                RotationId = rotationId,
                Start = DateTime.Now.AddHours(-1),
                End = DateTime.Now.AddHours(1)
            });

            // Act
            var result = _scheduler.GetActiveRotationId();

            // Assert
            Assert.Equal(rotationId, result);
        }

        [Fact]
        public void GetActiveRotationId_NoMatchingRule_ReturnsDefaultRotationId()
        {
            // Arrange
            var defaultRotationId = Guid.NewGuid();
            _mockSettingsService.SetupGet(s => s.DefaultRotationId).Returns(defaultRotationId);
            var rotationId = Guid.NewGuid();
            _rules.Add(new SimpleScheduleRule
            {
                RotationId = rotationId,
                Start = DateTime.Now.AddHours(1), // Starts in the future
                End = DateTime.Now.AddHours(2)
            });

            // Act
            var result = _scheduler.GetActiveRotationId();

            // Assert
            Assert.Equal(defaultRotationId, result);
        }

        [Fact]
        public void GetActiveRotationId_RuleWithNoEndDate_IsActive()
        {
            // Arrange
            var rotationId = Guid.NewGuid();
            _rules.Add(new SimpleScheduleRule
            {
                RotationId = rotationId,
                Start = DateTime.Now.AddMinutes(-10),
                End = null // Runs indefinitely
            });

            // Act
            var result = _scheduler.GetActiveRotationId();

            // Assert
            Assert.Equal(rotationId, result);
        }

        [Fact]
        public void GetActiveRotationId_MultipleMatchingRules_ReturnsFirstByStartDate()
        {
            // Arrange
            var firstRotationId = Guid.NewGuid();
            var secondRotationId = Guid.NewGuid();

            _rules.Add(new SimpleScheduleRule
            {
                RotationId = secondRotationId,
                Start = DateTime.Now.AddHours(-1), // Started 1 hour ago
                End = DateTime.Now.AddHours(1)
            });
            _rules.Add(new SimpleScheduleRule
            {
                RotationId = firstRotationId,
                Start = DateTime.Now.AddHours(-2), // Started 2 hours ago
                End = DateTime.Now.AddHours(1)
            });


            // Act
            var result = _scheduler.GetActiveRotationId();

            // Assert
            Assert.Equal(firstRotationId, result);
        }

        [Fact]
        public void GetActiveRotation_FindsCorrectRotationObject()
        {
            // Arrange
            var rotationId = Guid.NewGuid();
            var expectedRotation = new SimpleRotation { Id = rotationId, Name = "Test Rotation" };
            _rotations.Add(expectedRotation);
            _rules.Add(new SimpleScheduleRule
            {
                RotationId = rotationId,
                Start = DateTime.Now.AddHours(-1),
                End = DateTime.Now.AddHours(1)
            });

            // Act
            var result = _scheduler.GetActiveRotation();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Rotation", result.Name);
            Assert.Equal(rotationId, result.Id);
        }

        [Fact]
        public void GetActiveRotation_NoActiveRotation_ReturnsNull()
        {
            // Arrange
            _mockSettingsService.SetupGet(s => s.DefaultRotationId).Returns(Guid.Empty);

            // Act
            var result = _scheduler.GetActiveRotation();

            // Assert
            Assert.Null(result);
        }
    }
}
