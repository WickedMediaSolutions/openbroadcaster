using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class LibraryService
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".m4a", ".wma", ".ogg", ".aiff", ".aif", ".opus"
        };

        /// <summary>
        /// Permanent built-in TOH (Top-of-Hour) category IDs.
        /// These categories cannot be removed or renamed.
        /// </summary>
        public static readonly Guid TohCategoryStationIds = new("10000000-0000-0000-0000-000000000001");
        public static readonly Guid TohCategoryCommercials = new("10000000-0000-0000-0000-000000000002");
        public static readonly Guid TohCategoryJingles = new("10000000-0000-0000-0000-000000000003");

        /// <summary>
        /// Returns the set of permanent TOH category IDs.
        /// </summary>
        public static IReadOnlySet<Guid> TohCategoryIds { get; } = new HashSet<Guid>
        {
            TohCategoryStationIds,
            TohCategoryCommercials,
            TohCategoryJingles
        };

        /// <summary>
        /// Checks if the given category ID is a permanent TOH category.
        /// </summary>
        public static bool IsTohCategory(Guid categoryId) => TohCategoryIds.Contains(categoryId);

        private readonly object _syncRoot = new();
        private readonly string _filePath;
        private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
        private readonly Dictionary<Guid, Track> _tracks;
        private readonly Dictionary<Guid, LibraryCategory> _categories;
        private readonly Dictionary<string, Guid> _pathIndex;
        private readonly IAudioMetadataReader _metadataReader;

        public event EventHandler? TracksChanged;
        public event EventHandler? CategoriesChanged;

        public LibraryService(string? filePath = null, IAudioMetadataReader? metadataReader = null)
        {
            _filePath = filePath ?? ResolveDefaultLibraryPath();
            _metadataReader = metadataReader ?? new TagLibMetadataReader();

            if (filePath == null)
            {
                TryMigrateLegacyLibrary();
            }

            (_tracks, _categories, _pathIndex) = LoadSnapshot();

            // Ensure permanent TOH categories are persisted
            EnsureTohCategoriesPersisted();
        }

        /// <summary>
        /// Ensures permanent TOH categories are saved to disk if they were just created.
        /// </summary>
        private void EnsureTohCategoriesPersisted()
        {
            lock (_syncRoot)
            {
                // Check if all TOH categories exist
                bool hasAll = _categories.ContainsKey(TohCategoryStationIds)
                           && _categories.ContainsKey(TohCategoryCommercials)
                           && _categories.ContainsKey(TohCategoryJingles);
                
                if (hasAll)
                {
                    // Persist to ensure they're saved (idempotent)
                    PersistLocked();
                }
            }
        }

        public IReadOnlyCollection<Track> GetTracks()
        {
            lock (_syncRoot)
            {
                return _tracks.Values
                    .OrderBy(static track => track.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static track => track.Id)
                    .ToArray();
            }
        }

        public IReadOnlyList<Track> SearchTracks(string query, int limit)
        {
            if (string.IsNullOrWhiteSpace(query) || limit <= 0)
            {
                return Array.Empty<Track>();
            }

            var normalized = query.Trim();
            lock (_syncRoot)
            {
                return _tracks.Values
                    .Where(track => track.IsEnabled && MatchesSearch(track, normalized))
                    .OrderBy(track => track.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(track => track.Artist, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .ToArray();
            }
        }

        public IReadOnlyCollection<Track> GetTracksByCategory(Guid categoryId)
        {
            if (categoryId == Guid.Empty)
            {
                return Array.Empty<Track>();
            }

            lock (_syncRoot)
            {
                return _tracks.Values
                    .Where(track => track.CategoryIds.Contains(categoryId))
                    .OrderBy(static track => track.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static track => track.Id)
                    .ToArray();
            }
        }

        public IReadOnlyCollection<Track> GetAllTracks()
        {
            lock (_syncRoot)
            {
                return _tracks.Values
                    .OrderBy(static track => track.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static track => track.Id)
                    .ToArray();
            }
        }

        public IReadOnlyCollection<Track> GetUncategorizedTracks()
        {
            lock (_syncRoot)
            {
                return _tracks.Values
                    .Where(static track => track.CategoryIds == null || track.CategoryIds.Count == 0)
                    .OrderBy(static track => track.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static track => track.Id)
                    .ToArray();
            }
        }

        public Track? GetTrack(Guid trackId)
        {
            if (trackId == Guid.Empty)
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _tracks.TryGetValue(trackId, out var track) ? track : null;
            }
        }

        public IReadOnlyCollection<LibraryCategory> GetCategories()
        {
            lock (_syncRoot)
            {
                return _categories.Values
                    .OrderBy(static category => category.Type, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static category => category.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        public Track? ImportFile(string filePath, IEnumerable<Guid>? categoryIds = null)
        {
            var results = ImportFiles(new[] { filePath }, categoryIds);
            return results.FirstOrDefault();
        }

        public IReadOnlyCollection<Track> ImportFiles(IEnumerable<string>? filePaths, IEnumerable<Guid>? categoryIds = null)
        {
            if (filePaths == null)
            {
                return Array.Empty<Track>();
            }

            var normalizedPaths = NormalizeImportPaths(filePaths);
            if (normalizedPaths.Count == 0)
            {
                return Array.Empty<Track>();
            }

            var metadataPerFile = new Dictionary<string, LibraryTrackMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in normalizedPaths)
            {
                try
                {
                    metadataPerFile[path] = _metadataReader.ReadMetadata(path);
                }
                catch
                {
                    // Skip unreadable files but continue processing the rest.
                }
            }

            if (metadataPerFile.Count == 0)
            {
                return Array.Empty<Track>();
            }

            var added = new List<Track>();
            var updated = new List<Track>();
            lock (_syncRoot)
            {
                var normalizedCategories = ValidateCategoriesLocked(categoryIds);
                foreach (var kvp in metadataPerFile)
                {
                    // Check if track already exists
                    if (_pathIndex.TryGetValue(kvp.Key, out var existingId) && _tracks.TryGetValue(existingId, out var existingTrack))
                    {
                        // Track exists - merge categories if new ones are provided
                        if (normalizedCategories.Length > 0)
                        {
                            var mergedCategories = existingTrack.CategoryIds
                                .Concat(normalizedCategories)
                                .Distinct()
                                .ToArray();
                            
                            var updatedTrack = existingTrack.WithLibraryData(categoryIds: mergedCategories);
                            _tracks[existingId] = updatedTrack;
                            updated.Add(updatedTrack);
                        }
                        continue;
                    }

                    var metadata = kvp.Value;
                    var track = new Track(
                        metadata.Title,
                        metadata.Artist,
                        metadata.Album,
                        metadata.Genre,
                        metadata.Year,
                        metadata.Duration,
                        kvp.Key,
                        true,
                        normalizedCategories);

                    _tracks[track.Id] = track;
                    _pathIndex[kvp.Key] = track.Id;
                    added.Add(track);
                }

                if (added.Count > 0 || updated.Count > 0)
                {
                    PersistLocked();
                }
            }

            if (added.Count > 0 || updated.Count > 0)
            {
                OnTracksChanged();
            }

            // Return combined list - added tracks are new, updated tracks had categories merged
            return added.Concat(updated).ToList();
        }

        public IReadOnlyCollection<Track> ImportFolder(string folderPath, bool includeSubfolders = true, IEnumerable<Guid>? categoryIds = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return Array.Empty<Track>();
            }

            var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(folderPath, "*.*", option)
                .Where(IsSupportedExtension)
                .ToArray();

            return ImportFiles(files, categoryIds);
        }

        public LibraryCategory? GetCategory(Guid categoryId)
        {
            if (categoryId == Guid.Empty)
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _categories.TryGetValue(categoryId, out var category) ? category : null;
            }
        }

        public LibraryCategory AddCategory(string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Category name cannot be empty.", nameof(name));
            }

            if (IsReservedCategoryName(name))
            {
                throw new InvalidOperationException($"'{name}' is a reserved category name and cannot be used.");
            }

            var category = new LibraryCategory(name, type);
            var added = false;

            lock (_syncRoot)
            {
                if (_categories.Values.Any(existing =>
                        string.Equals(existing.Name, category.Name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(existing.Type, category.Type, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"A category named '{category.Name}' already exists for type '{category.Type}'.");
                }

                _categories[category.Id] = category;
                PersistLocked();
                added = true;
            }

            if (added)
            {
                OnCategoriesChanged();
            }

            return category;
        }

        public LibraryCategory UpdateCategory(Guid categoryId, string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Category name cannot be empty.", nameof(name));
            }

            // Permanent TOH categories cannot be renamed
            if (IsTohCategory(categoryId))
            {
                throw new InvalidOperationException("This is a permanent Top-of-Hour category and cannot be modified.");
            }

            if (IsReservedCategoryName(name))
            {
                throw new InvalidOperationException($"'{name}' is a reserved category name and cannot be used.");
            }

            LibraryCategory updated;
            var changed = false;
            lock (_syncRoot)
            {
                if (!_categories.TryGetValue(categoryId, out var existing))
                {
                    throw new KeyNotFoundException($"Category '{categoryId}' does not exist.");
                }

                updated = existing.WithMetadata(name, type);
                _categories[categoryId] = updated;
                PersistLocked();
                changed = true;
            }

            if (changed)
            {
                OnCategoriesChanged();
            }

            return updated;
        }

        public bool RemoveCategory(Guid categoryId)
        {
            if (categoryId == Guid.Empty)
            {
                return false;
            }

            // Permanent TOH categories cannot be removed
            if (IsTohCategory(categoryId))
            {
                throw new InvalidOperationException("This is a permanent Top-of-Hour category and cannot be removed.");
            }

            var removed = false;
            var affectedTracks = false;
            lock (_syncRoot)
            {
                if (!_categories.Remove(categoryId))
                {
                    return false;
                }
                removed = true;

                var affected = _tracks
                    .Where(pair => pair.Value.CategoryIds.Contains(categoryId))
                    .Select(pair => (pair.Key, pair.Value.WithLibraryData(categoryIds: pair.Value.CategoryIds.Where(id => id != categoryId))))
                    .ToList();

                foreach (var (key, updated) in affected)
                {
                    _tracks[key] = updated;
                }

                affectedTracks = affected.Count > 0;
                PersistLocked();
            }

            if (removed)
            {
                OnCategoriesChanged();
            }

            if (affectedTracks)
            {
                OnTracksChanged();
            }

            return true;
        }

        public Track SaveTrack(Track track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            Track normalizedTrack;
            bool changed;
            lock (_syncRoot)
            {
                var normalizedCategories = ValidateCategoriesLocked(track.CategoryIds);
                normalizedTrack = track.WithLibraryData(categoryIds: normalizedCategories);

                if (_tracks.TryGetValue(normalizedTrack.Id, out var existing))
                {
                    RemovePathIndexLocked(existing);
                }

                _tracks[normalizedTrack.Id] = normalizedTrack;
                UpdatePathIndexLocked(normalizedTrack);
                PersistLocked();
                changed = true;
            }

            if (changed)
            {
                OnTracksChanged();
            }

            return normalizedTrack;
        }

        public Track AssignCategories(Guid trackId, IEnumerable<Guid> categoryIds)
        {
            if (categoryIds == null)
            {
                throw new ArgumentNullException(nameof(categoryIds));
            }

            Track updated;
            bool changed;
            lock (_syncRoot)
            {
                if (!_tracks.TryGetValue(trackId, out var track))
                {
                    throw new KeyNotFoundException($"Track '{trackId}' does not exist.");
                }

                var normalizedCategories = ValidateCategoriesLocked(categoryIds);
                updated = track.WithLibraryData(categoryIds: normalizedCategories);
                _tracks[trackId] = updated;
                PersistLocked();
                changed = true;
            }

            if (changed)
            {
                OnTracksChanged();
            }

            return updated;
        }

        public Track SetTrackEnabled(Guid trackId, bool isEnabled)
        {
            Track updated;
            bool changed = false;
            lock (_syncRoot)
            {
                if (!_tracks.TryGetValue(trackId, out var track))
                {
                    throw new KeyNotFoundException($"Track '{trackId}' does not exist.");
                }

                if (track.IsEnabled == isEnabled)
                {
                    return track;
                }

                updated = track.WithLibraryData(isEnabled: isEnabled);
                _tracks[trackId] = updated;
                PersistLocked();
                changed = true;
            }

            if (changed)
            {
                OnTracksChanged();
            }

            return updated;
        }

        public bool RemoveTrack(Guid trackId)
        {
            if (trackId == Guid.Empty)
            {
                return false;
            }

            var removed = false;
            lock (_syncRoot)
            {
                if (_tracks.Remove(trackId, out var track))
                {
                    removed = true;
                    RemovePathIndexLocked(track);
                    PersistLocked();
                }
            }

            if (removed)
            {
                OnTracksChanged();
            }

            return removed;
        }

        private static bool MatchesSearch(Track track, string query)
        {
            return Contains(track.Title, query)
                || Contains(track.Artist, query)
                || Contains(track.Album, query)
                || Contains(track.Genre, query);
        }

        private static bool Contains(string? source, string query)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsReservedCategoryName(string name)
        {
            var reserved = new[] { "All Categories", "All", "Uncategorized" };
            return reserved.Any(r => string.Equals(r, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private Guid[] ValidateCategoriesLocked(IEnumerable<Guid>? categoryIds)
        {
            if (categoryIds == null)
            {
                return Array.Empty<Guid>();
            }

            var normalized = categoryIds
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            foreach (var categoryId in normalized)
            {
                if (!_categories.ContainsKey(categoryId))
                {
                    throw new KeyNotFoundException($"Category '{categoryId}' does not exist.");
                }
            }

            return normalized;
        }

        private static bool IsSupportedExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
        }

        private static List<string> NormalizeImportPaths(IEnumerable<string> filePaths)
        {
            var list = new List<string>();
            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    var normalized = NormalizePath(path);
                    if (File.Exists(normalized))
                    {
                        list.Add(normalized);
                    }
                }
                catch
                {
                    // Skip invalid paths
                }
            }

            return list;
        }

        private static string NormalizePath(string filePath)
        {
            return Path.GetFullPath(filePath ?? string.Empty).Trim();
        }

        private void UpdatePathIndexLocked(Track track)
        {
            if (string.IsNullOrWhiteSpace(track.FilePath))
            {
                return;
            }

            var normalized = NormalizePath(track.FilePath);
            _pathIndex[normalized] = track.Id;
        }

        private void RemovePathIndexLocked(Track track)
        {
            if (string.IsNullOrWhiteSpace(track.FilePath))
            {
                return;
            }

            var normalized = NormalizePath(track.FilePath);
            _pathIndex.Remove(normalized);
        }

        private void OnTracksChanged()
        {
            TracksChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnCategoriesChanged()
        {
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PersistLocked()
        {
            var payload = new LibraryPayload
            {
                Tracks = _tracks.Values.Select(LibraryTrackSnapshot.FromTrack).ToList(),
                Categories = _categories.Values.Select(LibraryCategorySnapshot.FromCategory).ToList()
            };

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(payload, _serializerOptions);
            File.WriteAllText(_filePath, json);
        }

        private static string ResolveDefaultLibraryPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var root = Path.Combine(appData, "OpenBroadcaster");
            return Path.Combine(root, "library.json");
        }

        private void TryMigrateLegacyLibrary()
        {
            var legacyPath = Path.Combine(AppContext.BaseDirectory, "library.json");
            if (File.Exists(_filePath) || !File.Exists(legacyPath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(legacyPath, _filePath, overwrite: false);
            }
            catch
            {
                // If migration fails we will recreate defaults on demand.
            }
        }

        private (Dictionary<Guid, Track> tracks, Dictionary<Guid, LibraryCategory> categories, Dictionary<string, Guid> pathIndex) LoadSnapshot()
        {
            if (!File.Exists(_filePath))
            {
                return (new Dictionary<Guid, Track>(), new Dictionary<Guid, LibraryCategory>(), new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
            }

            try
            {
                using var stream = File.OpenRead(_filePath);
                var payload = JsonSerializer.Deserialize<LibraryPayload>(stream, _serializerOptions);
                if (payload == null)
                {
                    return (new Dictionary<Guid, Track>(), new Dictionary<Guid, LibraryCategory>(), new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
                }

                var categories = new Dictionary<Guid, LibraryCategory>();
                if (payload.Categories != null)
                {
                    foreach (var category in payload.Categories)
                    {
                        if (category == null || string.IsNullOrWhiteSpace(category.Name))
                        {
                            continue;
                        }

                        var categoryId = category.Id == Guid.Empty ? Guid.NewGuid() : category.Id;
                        categories[categoryId] = new LibraryCategory(categoryId, category.Name, category.Type ?? "General");
                    }
                }

                var tracks = new Dictionary<Guid, Track>();
                if (payload.Tracks != null)
                {
                    foreach (var track in payload.Tracks)
                    {
                        if (track == null || string.IsNullOrWhiteSpace(track.Title))
                        {
                            continue;
                        }

                        var trackId = track.Id == Guid.Empty ? Guid.NewGuid() : track.Id;
                        var allowedCategories = track.CategoryIds?.Where(categories.ContainsKey).ToArray();
                        var duration = track.DurationTicks > 0 ? TimeSpan.FromTicks(track.DurationTicks) : TimeSpan.Zero;
                        tracks[trackId] = new Track(
                            trackId,
                            track.Title,
                            track.Artist ?? string.Empty,
                            track.Album ?? string.Empty,
                            track.Genre ?? string.Empty,
                            track.Year,
                            duration,
                            track.FilePath,
                            track.IsEnabled,
                            allowedCategories);
                    }
                }

                var pathIndex = BuildPathIndex(tracks);
                EnsureTohCategories(categories);
                return (tracks, categories, pathIndex);
            }
            catch
            {
                var emptyCategories = new Dictionary<Guid, LibraryCategory>();
                EnsureTohCategories(emptyCategories);
                return (new Dictionary<Guid, Track>(), emptyCategories, new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Ensures the permanent TOH categories exist in the given dictionary.
        /// </summary>
        private static void EnsureTohCategories(Dictionary<Guid, LibraryCategory> categories)
        {
            if (!categories.ContainsKey(TohCategoryStationIds))
            {
                categories[TohCategoryStationIds] = new LibraryCategory(TohCategoryStationIds, "Station IDs", "TOH");
            }
            if (!categories.ContainsKey(TohCategoryCommercials))
            {
                categories[TohCategoryCommercials] = new LibraryCategory(TohCategoryCommercials, "Commercials", "TOH");
            }
            if (!categories.ContainsKey(TohCategoryJingles))
            {
                categories[TohCategoryJingles] = new LibraryCategory(TohCategoryJingles, "Jingles", "TOH");
            }
        }

        private static Dictionary<string, Guid> BuildPathIndex(Dictionary<Guid, Track> tracks)
        {
            var index = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var track in tracks.Values)
            {
                if (string.IsNullOrWhiteSpace(track.FilePath))
                {
                    continue;
                }

                try
                {
                    var normalized = NormalizePath(track.FilePath);
                    if (!index.ContainsKey(normalized))
                    {
                        index[normalized] = track.Id;
                    }
                }
                catch
                {
                    // Skip invalid paths while rebuilding the index.
                }
            }

            return index;
        }

        private sealed class LibraryPayload
        {
            public List<LibraryTrackSnapshot> Tracks { get; set; } = new();
            public List<LibraryCategorySnapshot> Categories { get; set; } = new();
        }

        private sealed class LibraryTrackSnapshot
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Artist { get; set; }
            public string? Album { get; set; }
            public string? Genre { get; set; }
            public int Year { get; set; }
            public long DurationTicks { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
            public List<Guid>? CategoryIds { get; set; }

            public static LibraryTrackSnapshot FromTrack(Track track)
            {
                return new LibraryTrackSnapshot
                {
                    Id = track.Id,
                    Title = track.Title,
                    Artist = track.Artist,
                    Album = track.Album,
                    Genre = track.Genre,
                    Year = track.Year,
                    DurationTicks = track.Duration.Ticks,
                    FilePath = track.FilePath,
                    IsEnabled = track.IsEnabled,
                    CategoryIds = track.CategoryIds?.ToList()
                };
            }
        }

        private sealed class LibraryCategorySnapshot
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Type { get; set; }

            public static LibraryCategorySnapshot FromCategory(LibraryCategory category)
            {
                return new LibraryCategorySnapshot
                {
                    Id = category.Id,
                    Name = category.Name,
                    Type = category.Type
                };
            }
        }
    }
}
