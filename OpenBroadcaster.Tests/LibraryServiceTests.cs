using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class LibraryServiceTests
    {
        [Fact]
        public void LibraryService_PersistsTracksAndCategories()
        {
            var libraryPath = CreateTempLibraryPath();

            try
            {
                var service = new LibraryService(libraryPath);
                var category = service.AddCategory("Utility Beds", "Bed");
                var track = new Track(
                    "Weather Bed",
                    "Atmo Lab",
                    "Utility Beds",
                    "Bed",
                    2024,
                    TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30),
                    @"C:\\audio\\weather-bed.wav",
                    true,
                    new[] { category.Id });

                var saved = service.SaveTrack(track);

                var rehydrated = new LibraryService(libraryPath);
                var tracks = rehydrated.GetTracks();
                var categories = rehydrated.GetCategories();

                // 3 permanent TOH categories + 1 user-added category = 4
                Assert.Equal(4, categories.Count);
                Assert.Contains(categories, c => c.Id == category.Id);
                Assert.Single(tracks);

                var restored = tracks.First();
                Assert.Equal(saved.Id, restored.Id);
                Assert.Equal(saved.Title, restored.Title);
                Assert.Equal(saved.FilePath, restored.FilePath);
                Assert.True(restored.IsEnabled);
                Assert.Contains(category.Id, restored.CategoryIds);
            }
            finally
            {
                DeleteIfExists(libraryPath);
            }
        }

        [Fact]
        public void AssignCategories_ThrowsWhenCategoryMissing()
        {
            var libraryPath = CreateTempLibraryPath();

            try
            {
                var service = new LibraryService(libraryPath);
                var track = service.SaveTrack(new Track("Satellite", "WinAmp Society", "Graphite Nights", "Alt", 2024, TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(42)));
                var missingCategoryId = Guid.NewGuid();

                Assert.Throws<KeyNotFoundException>(() => service.AssignCategories(track.Id, new[] { missingCategoryId }));
            }
            finally
            {
                DeleteIfExists(libraryPath);
            }
        }

        [Fact]
        public void ImportFiles_AddsTracksAndPreventsDuplicates()
        {
            var workingDir = CreateTempDirectory();
            var libraryPath = Path.Combine(workingDir, "library.json");
            var audioPath = CreateTempAudioFile(workingDir, "intro.mp3");

            try
            {
                var metadata = new LibraryTrackMetadata("Intro", "Studio", "ID Pack", "ID", 2026, TimeSpan.FromSeconds(12));
                var reader = new FakeMetadataReader(new Dictionary<string, LibraryTrackMetadata>
                {
                    [audioPath] = metadata
                });

                var service = new LibraryService(libraryPath, reader);
                var firstImport = service.ImportFiles(new[] { audioPath });
                Assert.Single(firstImport);
                Assert.Equal("Intro", firstImport.First().Title);

                var secondImport = service.ImportFiles(new[] { audioPath });
                Assert.Empty(secondImport);
            }
            finally
            {
                DeleteDirectoryIfExists(workingDir);
            }
        }

        [Fact]
        public void ImportFiles_AssignsProvidedCategories()
        {
            var workingDir = CreateTempDirectory();
            var libraryPath = Path.Combine(workingDir, "library.json");
            var audioPath = CreateTempAudioFile(workingDir, "bed.wav");

            try
            {
                var metadata = new LibraryTrackMetadata("Weather Bed", "FX Lab", "Utility", "Bed", 2025, TimeSpan.FromSeconds(90));
                var reader = new FakeMetadataReader(new Dictionary<string, LibraryTrackMetadata>
                {
                    [audioPath] = metadata
                });

                var service = new LibraryService(libraryPath, reader);
                var category = service.AddCategory("Beds", "Production");
                var imported = service.ImportFiles(new[] { audioPath }, new[] { category.Id });

                Assert.Single(imported);
                var track = imported.First();
                Assert.Contains(category.Id, track.CategoryIds);
            }
            finally
            {
                DeleteDirectoryIfExists(workingDir);
            }
        }

        [Fact]
        public void SearchTracks_ReturnsMatchesRespectingLimit()
        {
            var libraryPath = CreateTempLibraryPath();

            try
            {
                var service = new LibraryService(libraryPath);
                service.SaveTrack(new Track("Neon Skyline", "City Choir", "Night Shift", "Indie", 2025, TimeSpan.FromMinutes(3)));
                service.SaveTrack(new Track("City Lights", "Neon Harbor", "Night Shift", "Indie", 2025, TimeSpan.FromMinutes(4)));
                service.SaveTrack(new Track("Harbor Lights", "Analog Youth", "Downtown", "Alt", 2024, TimeSpan.FromMinutes(3)));

                var results = service.SearchTracks("lights", 2);

                Assert.Equal(2, results.Count);
                Assert.All(results, track => Assert.Contains("lights", track.Title, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                DeleteIfExists(libraryPath);
            }
        }

        [Fact]
        public void SearchTracks_IgnoresDisabledEntries()
        {
            var libraryPath = CreateTempLibraryPath();

            try
            {
                var service = new LibraryService(libraryPath);
                service.SaveTrack(new Track("Hidden Gem", "Silent Radio", "Underground", "Alt", 2024, TimeSpan.FromMinutes(4), filePath: null, isEnabled: false));

                var results = service.SearchTracks("hidden", 5);

                Assert.Empty(results);
            }
            finally
            {
                DeleteIfExists(libraryPath);
            }
        }

        private static string CreateTempLibraryPath()
        {
            var fileName = $"openbroadcaster-library-{Guid.NewGuid():N}.json";
            return Path.Combine(Path.GetTempPath(), fileName);
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"openbroadcaster-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string CreateTempAudioFile(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, "unit-test-audio");
            return path;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private sealed class FakeMetadataReader : IAudioMetadataReader
        {
            private readonly IReadOnlyDictionary<string, LibraryTrackMetadata> _metadata;

            public FakeMetadataReader(IReadOnlyDictionary<string, LibraryTrackMetadata> metadata)
            {
                _metadata = metadata;
            }

            public LibraryTrackMetadata ReadMetadata(string filePath)
            {
                if (_metadata.TryGetValue(filePath, out var data))
                {
                    return data;
                }

                throw new FileNotFoundException($"No metadata registered for {filePath}", filePath);
            }
        }
    }
}
