using System.Collections.Generic;
using OpenBroadcaster.Core.Automation;

namespace OpenBroadcaster.Views
{
    public sealed class RotationDialog
    {
        public RotationDialog(SimpleRotation rotation, List<string> categoryOptions, IEnumerable<string> existingNames)
        {
        }

        public bool? ShowDialog() => true;
    }

    public sealed class SchedulerDialog
    {
        public SchedulerDialog(SimpleSchedulerEntry entry, List<SimpleRotation> rotations)
        {
        }

        public bool? ShowDialog() => true;
    }
}
