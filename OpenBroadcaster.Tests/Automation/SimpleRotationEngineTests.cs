using System.Collections.Generic;
using System.Linq;
using Moq;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests.Automation
{
    public class SimpleRotationEngineTests
    {
        private readonly Mock<ILibraryService> _mockLibraryService;
        private readonly SimpleRotationEngine _engine;
        private readonly List<Media> _library;

        public SimpleRotationEngineTests()
        {
            _mockLibraryService = new Mock<ILibraryService>();
            _library = new List<Media>();
            _mockLibraryService.Setup(s => s.GetAll()).Returns(_library);
            _engine = new SimpleRotationEngine(_mockLibraryService.Object);
        }

        [Fact]
        public void GetNextTrack_NullRotation_ReturnsNull()
        {
            // Act
            var result = _engine.GetNextTrack(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetNextTrack_RotationWithNoCategories_ReturnsNull()
        {
            // Arrange
            var rotation = new SimpleRotation { Name = "Empty Rotation", CategoryIds = new List<string>() };

            // Act
            var result = _engine.GetNextTrack(rotation);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetNextTrack_NoTracksInLibraryForCategories_ReturnsNull()
        {
            // Arrange
            _library.Add(new Media { Id = 1, Title = "Track 1", Categories = new List<string> { "Pop" } });
            var rotation = new SimpleRotation { Name = "Rock Rotation", CategoryIds = new List<string> { "Rock" } };

            // Act
            var result = _engine.GetNextTrack(rotation);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetNextTrack_OneTrackInRotation_ReturnsThatTrack()
        {
            // Arrange
            var track = new Media { Id = 1, Title = "Track 1", Categories = new List<string> { "Rock" } };
            _library.Add(track);
            var rotation = new SimpleRotation { Name = "Rock Rotation", CategoryIds = new List<string> { "Rock" } };

            // Act
            var result = _engine.GetNextTrack(rotation);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(track.Id, result.Id);
        }

        [Fact]
        public void GetNextTrack_OneTrackInRotation_ReturnsSameTrackRepeatedly()
        {
            // Arrange
            var track = new Media { Id = 1, Title = "Track 1", Categories = new List<string> { "Rock" } };
            _library.Add(track);
            var rotation = new SimpleRotation { Name = "Rock Rotation", CategoryIds = new List<string> { "Rock" } };

            // Act
            var result1 = _engine.GetNextTrack(rotation);
            var result2 = _engine.GetNextTrack(rotation);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(track.Id, result1.Id);
            Assert.Equal(track.Id, result2.Id);
        }

        [Fact]
        public void GetNextTrack_MultipleTracks_DoesNotRepeatImmediately()
        {
            // Arrange
            var track1 = new Media { Id = 1, Title = "Track 1", Categories = new List<string> { "Rock" } };
            var track2 = new Media { Id = 2, Title = "Track 2", Categories = new List<string> { "Rock" } };
            _library.Add(track1);
            _library.Add(track2);
            var rotation = new SimpleRotation { Name = "Rock Rotation", CategoryIds = new List<string> { "Rock" } };

            // Act
            var result1 = _engine.GetNextTrack(rotation);
            var result2 = _engine.GetNextTrack(rotation);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotEqual(result1.Id, result2.Id);
        }

        [Fact]
        public void GetNextTrack_PicksFromCorrectCategory()
        {
            // Arrange
            var track1 = new Media { Id = 1, Title = "Pop Song", Categories = new List<string> { "Pop" } };
            var track2 = new Media { Id = 2, Title = "Rock Song", Categories = new List<string> { "Rock" } };
            _library.Add(track1);
            _library.Add(track2);
            var rotation = new SimpleRotation { Name = "Rock Rotation", CategoryIds = new List<string> { "Rock" } };

            // Act
            // Run it a few times to be reasonably sure it's not picking the wrong one by chance.
            for (int i = 0; i < 10; i++)
            {
                var result = _engine.GetNextTrack(rotation);
                Assert.NotNull(result);
                Assert.Equal(2, result.Id);
            }
        }
        
        [Fact]
        public void GetNextTrack_TrackInMultipleCategories_IsSelected()
        {
            // Arrange
            var track = new Media { Id = 1, Title = "Crossover Hit", Categories = new List<string> { "Pop", "Rock" } };
            _library.Add(track);
            var rotation = new SimpleRotation { Name = "Rock Rotation", CategoryIds = new List<string> { "Rock" } };

            // Act
            var result = _engine.GetNextTrack(rotation);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(track.Id, result.Id);
        }
    }
}
