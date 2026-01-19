using System;

namespace OpenBroadcaster.Core.Automation
{
    /// <summary>
    /// Defines a rule for when a specific rotation should be active.
    /// </summary>
    public class SimpleScheduleRule
    {
        /// <summary>
        /// A unique identifier for the schedule rule.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The ID of the <see cref="SimpleRotation"/> that this rule activates.
        /// </summary>
        public Guid RotationId { get; set; }

        /// <summary>
        /// The date and time when this rule becomes active.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// The optional date and time when this rule deactivates. If null, the rule runs indefinitely from the start time.
        /// </summary>
        public DateTime? End { get; set; }
    }
}
