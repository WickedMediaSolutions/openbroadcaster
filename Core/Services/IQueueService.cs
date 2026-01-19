using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// Defines the interface for a service that manages the track queue.
    /// </summary>
    public interface IQueueService
    {
        /// <summary>
        /// Checks if the queue is empty.
        /// </summary>
        /// <returns>True if the queue has no tracks, otherwise false.</returns>
        bool IsQueueEmpty();

        /// <summary>
        /// Adds a media item to the end of the queue.
        /// </summary>
        /// <param name="media">The media item to enqueue.</param>
        void EnqueueTrack(Media media);

        /// <summary>
        /// Gets the number of items currently in the queue.
        /// </summary>
        int GetQueueCount();
    }
}
