using System;
using System.Collections.Generic;
using System.Linq;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class RotationCategoryDefinition
    {
        public RotationCategoryDefinition(string name, IEnumerable<Track> tracks)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Rotation category name is required.", nameof(name));
            }

            Name = name.Trim();
            Tracks = (tracks ?? Array.Empty<Track>())
                .Where(static track => track != null)
                .ToList();
        }

        public string Name { get; }
        public IReadOnlyList<Track> Tracks { get; }
    }
}
