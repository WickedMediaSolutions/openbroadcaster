using System.Collections.Generic;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// Defines the interface for a service that manages the media library.
    /// </summary>
    public interface ILibraryService
    {
        /// <summary>
        /// Retrieves all media items from the library.
        /// </summary>
        /// <returns>A list of all media items.</returns>
        List<Media> GetAll();
    }
}
