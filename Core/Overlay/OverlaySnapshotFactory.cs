using System;
using System.Collections.Generic;
using System.Linq;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Overlay
{
    public sealed class OverlaySnapshotFactory
    {
        private OverlaySettings _settings = new();
        private string? _currentTrackFilePath;

        /// <summary>
        /// Gets the file path of the currently playing track (for artwork extraction).
        /// </summary>
        public string? CurrentTrackFilePath => _currentTrackFilePath;

        public void UpdateSettings(OverlaySettings? settings)
        {
            _settings = settings?.Clone() ?? new OverlaySettings();
        }

        public OverlayStateSnapshot Create(
            IEnumerable<OverlayDeckState> deckStates,
            IReadOnlyList<QueueItem> queueItems,
            IReadOnlyList<QueueItem> historyItems)
        {
            var decks = deckStates?.ToList() ?? new List<OverlayDeckState>();
            var queue = queueItems ?? Array.Empty<QueueItem>();
            var history = historyItems ?? Array.Empty<QueueItem>();

            // Find the playing deck first (actual status = Playing)
            var nowPlayingCandidate = decks
                .Where(state => state.QueueItem != null && state.IsPlaying)
                .FirstOrDefault();

            // Fallback: if no deck is marked as playing, use the first one with a track loaded
            // Don't order by ID - just use the first one found
            nowPlayingCandidate ??= decks
                .Where(state => state.QueueItem != null)
                .FirstOrDefault();

            // Track the current playing file for artwork extraction
            _currentTrackFilePath = nowPlayingCandidate?.QueueItem?.Track?.FilePath;

            var nowPlaying = BuildDeckPayload(nowPlayingCandidate, isNowPlaying: true);
            var nextTrack = BuildQueuePayload(queue.FirstOrDefault(), false, string.Empty, null, null, isNowPlaying: false);

            var historyLimit = Math.Clamp(_settings.RecentListLimit, 1, 20);
            var recent = history
                .Take(historyLimit)
                .Select(item => BuildQueuePayload(item, false, string.Empty, null, null, isNowPlaying: false))
                .Where(payload => payload != null)
                .Select(payload => payload!)
                .ToList();

            var requestLimit = Math.Clamp(_settings.RequestListLimit, 1, 20);
            var requests = queue
                .Where(IsRequestCandidate)
                .Take(requestLimit)
                .Select(item => BuildQueuePayload(item, false, string.Empty, null, null, isNowPlaying: false))
                .Where(payload => payload != null)
                .Select(payload => payload!)
                .ToList();

            return new OverlayStateSnapshot(
                DateTime.UtcNow,
                nowPlaying,
                nextTrack,
                recent,
                requests);
        }

        private OverlayTrackPayload? BuildDeckPayload(OverlayDeckState? deckState, bool isNowPlaying)
        {
            if (deckState == null)
            {
                return null;
            }

            return BuildQueuePayload(
                deckState.QueueItem,
                deckState.IsPlaying,
                deckState.DeckId.ToString(),
                deckState.Elapsed,
                deckState.Remaining,
                isNowPlaying);
        }

        private OverlayTrackPayload? BuildQueuePayload(
            QueueItem? item,
            bool isPlaying,
            string deck,
            TimeSpan? elapsed,
            TimeSpan? remaining,
            bool isNowPlaying)
        {
            if (item?.Track == null)
            {
                return null;
            }

            var durationSeconds = Math.Max(0, item.Track.Duration.TotalSeconds);
            var elapsedSeconds = ToSeconds(elapsed);
            var remainingSeconds = ToSeconds(remaining);

            return new OverlayTrackPayload(
                item.Track.Title,
                item.Track.Artist,
                item.Track.Album,
                item.Source,
                item.RequestedBy,
                deck,
                durationSeconds,
                elapsedSeconds,
                remainingSeconds,
                isPlaying,
                ResolveArtworkUrl(item, isNowPlaying));
        }

        private string ResolveArtworkUrl(QueueItem item, bool isNowPlaying)
        {
            // For now playing track, use dynamic track artwork endpoint with cache-busting parameter
            if (isNowPlaying && !string.IsNullOrWhiteSpace(item?.Track?.FilePath))
            {
                // Add track ID as query parameter to force browser to fetch new image when track changes
                return $"{OverlayPaths.TrackArtwork}?t={item.Track.Id}";
            }

            var url = _settings.ArtworkFallbackUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                if (!string.IsNullOrWhiteSpace(_settings.ArtworkFallbackFilePath))
                {
                    return OverlayPaths.CustomArtwork;
                }

                return OverlayPaths.DefaultArtwork;
            }

            return url;
        }

        private static bool IsRequestCandidate(QueueItem item)
        {
            if (item == null)
            {
                return false;
            }

            return item.HasRequester || item.SourceType == QueueSource.Twitch;
        }

        private static double? ToSeconds(TimeSpan? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return Math.Max(0, value.Value.TotalSeconds);
        }
    }

    public sealed record OverlayDeckState(
        DeckIdentifier DeckId,
        QueueItem? QueueItem,
        bool IsPlaying,
        TimeSpan Elapsed,
        TimeSpan Remaining);
}
