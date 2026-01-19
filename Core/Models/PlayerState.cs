using System;

namespace OpenBroadcaster.Core.Models
{
    /// <summary>
    /// Represents the current state of a media player.
    /// This is a placeholder definition based on the AutoDJ requirements.
    /// </summary>
    public class PlayerState
    {
        /// <summary>
        /// Gets or sets a value indicating whether the player is currently playing.
        /// </summary>
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Gets or sets the remaining time of the currently playing track.
        /// </summary>
        public TimeSpan TimeRemaining { get; set; }

        /// <summary>
        /// The ID of the currently playing media. Can be null if nothing is playing.
        /// </summary>
        public int? CurrentMediaId { get; set; }
    }
}
