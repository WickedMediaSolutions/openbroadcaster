using System;
using System.Collections.Generic;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class VuMeterService : IDisposable
    {
        private const double DecayFactor = 0.85;
        private readonly AudioRoutingGraph _graph;
        private readonly Dictionary<AudioBus, double> _levels = new();
        private readonly object _sync = new();
        private readonly Timer _decayTimer;

        public VuMeterService(AudioRoutingGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
            {
                _levels[bus] = 0;
            }

            _decayTimer = new Timer(120);
            _decayTimer.Elapsed += OnDecayTick;
            _decayTimer.AutoReset = true;
            _decayTimer.Start();
        }

        public event EventHandler<VuMeterReading>? VuMetersUpdated;

        public void UpdateSourceLevel(AudioSourceType source, double level)
        {
            lock (_sync)
            {
                foreach (var bus in _graph.GetRoute(source))
                {
                    _levels[bus] = Math.Max(level, _levels[bus]);
                }

                EmitLocked();
            }
        }

        private void OnDecayTick(object? sender, ElapsedEventArgs e)
        {
            lock (_sync)
            {
                bool changed = false;
                foreach (var bus in _levels.Keys)
                {
                    var decayed = _levels[bus] * DecayFactor;
                    if (Math.Abs(decayed - _levels[bus]) > 0.0001)
                    {
                        _levels[bus] = decayed;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EmitLocked();
                }
            }
        }

        private void EmitLocked()
        {
            var reading = new VuMeterReading(
                GetLevel(AudioBus.Program),
                GetLevel(AudioBus.Encoder),
                GetLevel(AudioBus.Mic));

            VuMetersUpdated?.Invoke(this, reading);
        }

        private double GetLevel(AudioBus bus) => _levels.TryGetValue(bus, out var level) ? level : 0;

        public void Dispose()
        {
            _decayTimer.Stop();
            _decayTimer.Dispose();
        }
    }
}
