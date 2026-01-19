using System;
using System.Collections.Generic;

namespace OpenBroadcaster.Core.Overlay
{
    public sealed class OverlayStateSnapshot
    {
        public static OverlayStateSnapshot Empty { get; } = new OverlayStateSnapshot(
            DateTime.UtcNow,
            null,
            null,
            Array.Empty<OverlayTrackPayload>(),
            Array.Empty<OverlayTrackPayload>());

        public OverlayStateSnapshot(
            DateTime generatedAtUtc,
            OverlayTrackPayload? nowPlaying,
            OverlayTrackPayload? nextTrack,
            IReadOnlyList<OverlayTrackPayload> recent,
            IReadOnlyList<OverlayTrackPayload> requests)
        {
            GeneratedAtUtc = generatedAtUtc;
            NowPlaying = nowPlaying;
            NextTrack = nextTrack;
            Recent = recent ?? Array.Empty<OverlayTrackPayload>();
            Requests = requests ?? Array.Empty<OverlayTrackPayload>();
        }

        public DateTime GeneratedAtUtc { get; }
        public OverlayTrackPayload? NowPlaying { get; }
        public OverlayTrackPayload? NextTrack { get; }
        public IReadOnlyList<OverlayTrackPayload> Recent { get; }
        public IReadOnlyList<OverlayTrackPayload> Requests { get; }
    }

    public sealed class OverlayTrackPayload
    {
        public OverlayTrackPayload(
            string title,
            string artist,
            string album,
            string source,
            string requestedBy,
            string deck,
            double durationSeconds,
            double? elapsedSeconds,
            double? remainingSeconds,
            bool isPlaying,
            string artworkUrl)
        {
            Title = title ?? string.Empty;
            Artist = artist ?? string.Empty;
            Album = album ?? string.Empty;
            Source = source ?? string.Empty;
            RequestedBy = requestedBy ?? string.Empty;
            Deck = deck ?? string.Empty;
            DurationSeconds = durationSeconds;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            IsPlaying = isPlaying;
            ArtworkUrl = artworkUrl ?? string.Empty;
        }

        public string Title { get; }
        public string Artist { get; }
        public string Album { get; }
        public string Source { get; }
        public string RequestedBy { get; }
        public string Deck { get; }
        public double DurationSeconds { get; }
        public double? ElapsedSeconds { get; }
        public double? RemainingSeconds { get; }
        public bool IsPlaying { get; }
        public string ArtworkUrl { get; }
    }
}
