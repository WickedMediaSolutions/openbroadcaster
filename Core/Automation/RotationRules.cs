using System;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class RotationRules
    {
        public int MinArtistSeparation { get; init; } = 3;
        public int MinTitleSeparation { get; init; } = 6;
        public TimeSpan MinArtistCooldown { get; init; } = TimeSpan.FromMinutes(30);
        public TimeSpan MinTitleCooldown { get; init; } = TimeSpan.FromMinutes(60);
        public int HistoryLimit { get; init; } = 40;
    }
}
