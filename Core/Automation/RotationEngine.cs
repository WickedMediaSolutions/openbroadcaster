using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class RotationEngine
    {
        private readonly Dictionary<string, RotationCategoryState> _categories = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<RotationHistoryItem> _history = new();
        private readonly object _sync = new();
        private readonly RotationRules _rules;
        private readonly ILogger<RotationEngine> _logger;

        public RotationEngine(RotationRules? rules = null, ILogger<RotationEngine>? logger = null)
        {
            _rules = rules ?? new RotationRules();
            _logger = logger ?? AppLogger.CreateLogger<RotationEngine>();
        }

        public void LoadCategories(IEnumerable<RotationCategoryDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            lock (_sync)
            {
                _categories.Clear();
                foreach (var definition in definitions)
                {
                    AddOrUpdateCategory(definition);
                }
            }
        }

        public void UpdateCategory(RotationCategoryDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            lock (_sync)
            {
                AddOrUpdateCategory(definition);
            }
        }

        public Track? NextTrack(string categoryName)
        {
            if (!TryGetCategory(categoryName, out var category))
            {
                return null;
            }

            lock (_sync)
            {
                var selection = SelectNextTrack(category, enforceRules: true);
                if (selection.Track != null)
                {
                    category.Pointer = selection.NextPointer;
                    AppendHistory(selection.Track);
                }

                return selection.Track;
            }
        }

        public Track? PeekNextTrack(string categoryName)
        {
            if (!TryGetCategory(categoryName, out var category))
            {
                return null;
            }

            lock (_sync)
            {
                var selection = SelectNextTrack(category, enforceRules: true);
                return selection.Track;
            }
        }

        private bool TryGetCategory(string categoryName, out RotationCategoryState category)
        {
            category = null!;
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            lock (_sync)
            {
                return _categories.TryGetValue(categoryName.Trim(), out category!);
            }
        }

        private RotationSelection SelectNextTrack(RotationCategoryState category, bool enforceRules)
        {
            if (category.Tracks.Count == 0)
            {
                return RotationSelection.None;
            }

            for (var offset = 0; offset < category.Tracks.Count; offset++)
            {
                var index = (category.Pointer + offset) % category.Tracks.Count;
                var candidate = category.Tracks[index];
                if (!enforceRules || IsCandidateAllowed(candidate))
                {
                    var nextPointer = (index + 1) % category.Tracks.Count;
                    return new RotationSelection(candidate, nextPointer);
                }
            }

            _logger.LogWarning("Rotation rules prevented selection in category {Category}. Falling back to pointer track.", category.Name);
            var fallback = category.Tracks[category.Pointer];
            var pointer = (category.Pointer + 1) % category.Tracks.Count;
            return new RotationSelection(fallback, pointer);
        }

        private bool IsCandidateAllowed(Track track)
        {
            if (track == null)
            {
                return false;
            }

            if (_rules.MinArtistSeparation <= 0 && _rules.MinTitleSeparation <= 0)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            var artist = track.Artist ?? string.Empty;
            var title = track.Title ?? string.Empty;
            var historyList = _history.ToList();

            for (var i = 0; i < historyList.Count; i++)
            {
                var entry = historyList[i];
                if (_rules.MinArtistSeparation > 0 && i < _rules.MinArtistSeparation && string.Equals(entry.Artist, artist, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (_rules.MinTitleSeparation > 0 && i < _rules.MinTitleSeparation && string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (_rules.MinArtistCooldown > TimeSpan.Zero && string.Equals(entry.Artist, artist, StringComparison.OrdinalIgnoreCase))
                {
                    if (now - entry.PlayedAtUtc < _rules.MinArtistCooldown)
                    {
                        return false;
                    }
                }

                if (_rules.MinTitleCooldown > TimeSpan.Zero && string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase))
                {
                    if (now - entry.PlayedAtUtc < _rules.MinTitleCooldown)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void AppendHistory(Track track)
        {
            _history.AddFirst(new RotationHistoryItem(track.Title, track.Artist, DateTime.UtcNow));
            while (_history.Count > Math.Max(_rules.HistoryLimit, (_rules.MinArtistSeparation + _rules.MinTitleSeparation) * 2))
            {
                _history.RemoveLast();
            }
        }

        private void AddOrUpdateCategory(RotationCategoryDefinition definition)
        {
            if (!_categories.TryGetValue(definition.Name, out var existing))
            {
                existing = new RotationCategoryState(definition.Name, new List<Track>(definition.Tracks));
                _categories[definition.Name] = existing;
            }
            else
            {
                existing.Tracks.Clear();
                existing.Tracks.AddRange(definition.Tracks);
                existing.Pointer = 0;
            }
        }

        private sealed class RotationCategoryState
        {
            public RotationCategoryState(string name, List<Track> tracks)
            {
                Name = name;
                Tracks = tracks;
            }

            public string Name { get; }
            public List<Track> Tracks { get; }
            public int Pointer { get; set; }
        }

        private sealed record RotationHistoryItem(string Title, string Artist, DateTime PlayedAtUtc);

        private readonly record struct RotationSelection(Track? Track, int NextPointer)
        {
            public static RotationSelection None => new(null, 0);
        }
    }
}
