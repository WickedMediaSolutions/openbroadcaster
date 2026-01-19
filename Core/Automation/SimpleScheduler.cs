using System;
using System.Linq;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Automation
{
    /// <summary>
    /// Determines which rotation is active based on a schedule.
    /// </summary>
    public class SimpleScheduler
    {
        private readonly AutoDjSettingsService _settingsService;

        public SimpleScheduler(AutoDjSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Gets the ID of the currently active rotation based on the schedule.
        /// Falls back to the default rotation if no scheduled rule matches.
        /// </summary>
        /// <returns>The GUID of the active rotation.</returns>
        public virtual Guid GetActiveRotationId()
        {
            var now = DateTime.Now;
            var activeRule = _settingsService.Rules
                .OrderBy(rule => rule.Start) // Prioritize older rules just in case of overlap
                .FirstOrDefault(rule =>
                    now >= rule.Start && (!rule.End.HasValue || now <= rule.End.Value));

            if (activeRule != null)
            {
                return activeRule.RotationId;
            }

            return _settingsService.DefaultRotationId;
        }

        /// <summary>
        /// Gets the full <see cref="SimpleRotation"/> object for the active rotation.
        /// </summary>
        /// <returns>The active rotation object, or null if none is active or found.</returns>
        public virtual SimpleRotation? GetActiveRotation()
        {
            var rotationId = GetActiveRotationId();
            if (rotationId == Guid.Empty)
            {
                return null;
            }
            return _settingsService.Rotations.FirstOrDefault(r => r.Id == rotationId);
        }
    }
}
