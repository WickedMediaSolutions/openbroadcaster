using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// Defines the interface for a service that provides the status of the media player.
    /// </summary>
    public interface IPlayerStatusService
    {
        /// <summary>
        /// Gets the current state of the player.
        /// </summary>
        /// <returns>A <see cref="PlayerState"/> object with the latest information.</returns>
        PlayerState GetPlayerState();
    }
}
