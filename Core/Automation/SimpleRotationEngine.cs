using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Automation
{
    /// <summary>
    /// Selects tracks from a rotation based on simple, random logic.
    /// </summary>
    public class SimpleRotationEngine
    {
        private readonly ILibraryService _libraryService;
        private readonly ILogger<SimpleRotationEngine> _logger;
        private int? _lastTrackId;

        public SimpleRotationEngine(ILibraryService libraryService)
        {
            _libraryService = libraryService;
            _logger = AppLogger.CreateLogger<SimpleRotationEngine>();
            _lastTrackId = null;
        }

        /// <summary>
        /// Gets the next track to play from the given rotation.
        /// It avoids playing the same track back-to-back if other options are available.
        /// </summary>
        /// <param name="rotation">The rotation to select a track from.</param>
        /// <returns>A random <see cref="Media"/> from the rotation, or null if none are available.</returns>
        public virtual Media? GetNextTrack(SimpleRotation? rotation)
        {
            if (rotation == null || rotation.CategoryIds == null || !rotation.CategoryIds.Any())
            {
                _logger.LogWarning("Rotation is null or has no categories");
                return null;
            }

            var allTracks = _libraryService.GetAll();
            if (allTracks == null)
            {
                _logger.LogWarning("Library service returned no tracks");
                return null;
            }
            
            var categoryIdHashSet = new HashSet<string>(rotation.CategoryIds);
            
            var tracksInRotation = allTracks
                .Where(track => track.Categories != null && track.Categories.Any(categoryIdHashSet.Contains))
                .ToList();

            if (!tracksInRotation.Any())
            {
                _logger.LogWarning("No tracks found for rotation '{RotationName}'", rotation.Name);
                return null;
            }

            var eligibleTracks = tracksInRotation;
            if (_lastTrackId.HasValue)
            {
                var tracksWithoutLast = tracksInRotation.Where(t => t.Id != _lastTrackId.Value).ToList();
                if (tracksWithoutLast.Any())
                {
                    eligibleTracks = tracksWithoutLast;
                }
                // If filtering the last track leaves an empty list (i.e., only one track in rotation),
                // we knowingly play it again by falling back to the original 'tracksInRotation' list (via 'eligibleTracks').
            }

            var random = new Random();
            var nextTrack = eligibleTracks.ElementAt(random.Next(eligibleTracks.Count));

            _lastTrackId = nextTrack.Id;

            return nextTrack;
        }
    }
}
