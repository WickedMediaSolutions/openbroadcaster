using System;
using System.Collections.Generic;

namespace OpenBroadcaster.Core.Models
{
    /// <summary>
    /// Represents a media item in the library, such as a song.
    /// This is a placeholder definition based on the AutoDJ requirements.
    /// </summary>
    public class Media
    {
        /// <summary>
        /// The unique ID of the media item.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The title of the media item.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The artist of the media item.
        /// </summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// The duration of the media item.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// A list of category IDs this media item belongs to.
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();
    }
}
