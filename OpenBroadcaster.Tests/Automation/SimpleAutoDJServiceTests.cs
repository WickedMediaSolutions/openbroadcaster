using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests.Automation
{
    public class SimpleAutoDJServiceTests : IDisposable
    {
        private readonly Mock<SimpleScheduler> _mockScheduler;
        private readonly Mock<SimpleRotationEngine> _mockRotationEngine;
        private readonly Mock<IQueueService> _mockQueueService;
        private readonly Mock<IPlayerStatusService> _mockPlayerStatusService;
        private readonly SimpleAutoDJService _autoDjService;
        private readonly SimpleRotation _defaultRotation;
        private readonly Media _nextTrack;

        public SimpleAutoDJServiceTests()
        {
            // Mocking the dependencies of SimpleAutoDJService
            var mockSettings = new Mock<AutoDjSettingsService>(false);
            _mockScheduler = new Mock<SimpleScheduler>(mockSettings.Object);
            
            var mockLibrary = new Mock<ILibraryService>();
            _mockRotationEngine = new Mock<SimpleRotationEngine>(mockLibrary.Object);

            _mockQueueService = new Mock<IQueueService>();
            _mockPlayerStatusService = new Mock<IPlayerStatusService>();

            _autoDjService = new SimpleAutoDJService(
                _mockScheduler.Object,
                _mockRotationEngine.Object,
                _mockQueueService.Object,
                _mockPlayerStatusService.Object
            );

            // Common Arrange setups
            _defaultRotation = new SimpleRotation { Id = Guid.NewGuid(), Name = "Default" };
            _nextTrack = new Media { Id = 123, Title = "Next Up" };

            _mockScheduler.Setup(s => s.GetActiveRotation()).Returns(_defaultRotation);
            _mockRotationEngine.Setup(re => re.GetNextTrack(_defaultRotation)).Returns(_nextTrack);
            _mockQueueService.Setup(qs => qs.IsQueueEmpty()).Returns(true);
        }

        [Fact]
        public void IsEnabled_SetToTrue_StartsTimer()
        {
            // This is hard to test without exposing the timer or using a wrapper.
            // We'll test the functional outcome instead: does it queue a track when enabled?
            
            // Arrange
            _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = false, TimeRemaining = TimeSpan.Zero });

            // Act
            _autoDjService.IsEnabled = true;
            // Give timer a moment to fire. A more robust solution would be a manual timer control.
            Thread.Sleep(50); 

            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(_nextTrack), Times.Once);
        }

        [Fact]
        public void IsEnabled_SetToFalse_DoesNotQueueTrack()
        {
            // Arrange
            _autoDjService.IsEnabled = true; // Enable first
            _autoDjService.IsEnabled = false; // Then disable
            
            _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = false, TimeRemaining = TimeSpan.Zero });

            // Act
            Thread.Sleep(50); // Wait to see if timer fires

            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(It.IsAny<Media>()), Times.Never);
        }
        
        [Fact]
        public void CheckAndQueueNextTrack_PlayerStoppedAndQueueEmpty_QueuesTrack()
        {
            // Arrange
            _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = false, TimeRemaining = TimeSpan.Zero });
            _autoDjService.IsEnabled = true;
            
            // Act
            Thread.Sleep(50);
            
            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(_nextTrack), Times.Once);
        }

        [Fact]
        public void CheckAndQueueNextTrack_TrackNearingEnd_QueuesTrack()
        {
            // Arrange
             _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = true, TimeRemaining = TimeSpan.FromSeconds(10), CurrentMediaId = 1 });
            _autoDjService.IsEnabled = true;
            
            // Act
            Thread.Sleep(50);
            
            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(_nextTrack), Times.Once);
        }
        
        [Fact]
        public void CheckAndQueueNextTrack_TrackNotNearEnd_DoesNotQueue()
        {
            // Arrange
            _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = true, TimeRemaining = TimeSpan.FromSeconds(60) });
            _autoDjService.IsEnabled = true;
            
            // Act
            Thread.Sleep(50);
            
            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(It.IsAny<Media>()), Times.Never);
        }
        
        [Fact]
        public void CheckAndQueueNextTrack_QueueIsNotEmpty_DoesNotQueue()
        {
            // Arrange
            _mockQueueService.Setup(qs => qs.IsQueueEmpty()).Returns(false);
            _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = true, TimeRemaining = TimeSpan.FromSeconds(10) });
            _autoDjService.IsEnabled = true;
            
            // Act
            Thread.Sleep(50);

            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(It.IsAny<Media>()), Times.Never);
        }

        [Fact]
        public void CheckAndQueueNextTrack_NoActiveRotation_DoesNotQueue()
        {
            // Arrange
            _mockScheduler.Setup(s => s.GetActiveRotation()).Returns((SimpleRotation?)null);
            _mockPlayerStatusService.Setup(ps => ps.GetPlayerState())
                .Returns(new PlayerState { IsPlaying = false });
            _autoDjService.IsEnabled = true;

            // Act
            Thread.Sleep(50);

            // Assert
            _mockQueueService.Verify(qs => qs.EnqueueTrack(It.IsAny<Media>()), Times.Never);
            Assert.Equal("None", _autoDjService.ActiveRotationName);
        }

        public void Dispose()
        {
            _autoDjService.Dispose();
        }
    }
}
