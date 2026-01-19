using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class AudioRoutingGraph
    {
        private readonly Dictionary<AudioSourceType, HashSet<AudioBus>> _routes = new();
        private readonly object _sync = new();

        public AudioRoutingGraph()
        {
            Route(AudioSourceType.DeckA, AudioBus.Program, AudioBus.Encoder);
            Route(AudioSourceType.DeckB, AudioBus.Program, AudioBus.Encoder);
            Route(AudioSourceType.Cartwall, AudioBus.Program, AudioBus.Encoder);
            Route(AudioSourceType.Microphone, AudioBus.Encoder, AudioBus.Mic);
        }

        public void Route(AudioSourceType source, params AudioBus[] buses)
        {
            lock (_sync)
            {
                if (!_routes.TryGetValue(source, out var set))
                {
                    set = new HashSet<AudioBus>();
                    _routes[source] = set;
                }

                set.Clear();
                if (buses == null || buses.Length == 0)
                {
                    return;
                }

                foreach (var bus in buses)
                {
                    set.Add(bus);
                }
            }
        }

        public IReadOnlyList<AudioBus> GetRoute(AudioSourceType source)
        {
            lock (_sync)
            {
                if (_routes.TryGetValue(source, out var set))
                {
                    return set.ToList();
                }
            }

            return Array.Empty<AudioBus>();
        }
    }
}
