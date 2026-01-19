using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Models
{
    public sealed class Track
    {
        private readonly Guid[] _categoryIds;

        public Track(string title, string artist, string album, string genre, int year, TimeSpan duration)
            : this(Guid.NewGuid(), title, artist, album, genre, year, duration, string.Empty, true, null)
        {
        }

        public Track(
            string title,
            string artist,
            string album,
            string genre,
            int year,
            TimeSpan duration,
            string? filePath,
            bool isEnabled,
            IEnumerable<Guid>? categoryIds = null)
            : this(Guid.NewGuid(), title, artist, album, genre, year, duration, filePath, isEnabled, categoryIds)
        {
        }

        [JsonConstructor]
        public Track(
            Guid id,
            string title,
            string artist,
            string album,
            string genre,
            int year,
            TimeSpan duration,
            string? filePath,
            bool isEnabled,
            IEnumerable<Guid>? categoryIds)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Track title is required.", nameof(title));
            }

            Id = id == Guid.Empty ? Guid.NewGuid() : id;
            Title = title;
            Artist = artist ?? string.Empty;
            Album = album ?? string.Empty;
            Genre = genre ?? string.Empty;
            Year = year;
            Duration = duration;
            FilePath = filePath ?? string.Empty;
            IsEnabled = isEnabled;
            _categoryIds = NormalizeCategoryIds(categoryIds);
        }

        public Guid Id { get; }
        public string Title { get; }
        public string Artist { get; }
        public string Album { get; }
        public string Genre { get; }
        public int Year { get; }
        public TimeSpan Duration { get; }
        public string FilePath { get; }
        public bool IsEnabled { get; }
        public IReadOnlyCollection<Guid> CategoryIds => _categoryIds;

        public Track WithLibraryData(string? filePath = null, bool? isEnabled = null, IEnumerable<Guid>? categoryIds = null)
        {
            return new Track(
                Id,
                Title,
                Artist,
                Album,
                Genre,
                Year,
                Duration,
                filePath ?? FilePath,
                isEnabled ?? IsEnabled,
                categoryIds ?? _categoryIds);
        }

        private static Guid[] NormalizeCategoryIds(IEnumerable<Guid>? categoryIds)
        {
            if (categoryIds == null)
            {
                return Array.Empty<Guid>();
            }

            return categoryIds
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToArray();
        }
    }
}
