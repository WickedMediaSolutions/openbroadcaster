using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBroadcaster.Core.Automation
{
    public sealed class ClockwheelScheduler
    {
        private readonly List<ClockwheelSlot> _slots = new();
        private readonly object _sync = new();
        private readonly Dictionary<string, RotationState> _rotationStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<DayOfWeek, List<RotationScheduleEntry>> _scheduleByDay = new();
        private int _pointer;
        private string _defaultRotationName = string.Empty;

        public void LoadSlots(IEnumerable<ClockwheelSlot> slots)
        {
            if (slots == null)
            {
                return;
            }

            lock (_sync)
            {
                _rotationStates.Clear();
                _scheduleByDay.Clear();
                _defaultRotationName = string.Empty;
                _slots.Clear();
                _slots.AddRange(slots.Where(static slot => slot != null));
                _pointer = 0;
            }
        }

        public void ConfigureRotations(IEnumerable<RotationProgram>? programs, IEnumerable<RotationScheduleEntry>? schedule, string? defaultRotationName)
        {
            lock (_sync)
            {
                _slots.Clear();
                _pointer = 0;
                _rotationStates.Clear();
                _scheduleByDay.Clear();

                if (programs != null)
                {
                    foreach (var program in programs)
                    {
                        if (program == null || !program.HasSlots)
                        {
                            continue;
                        }

                        var name = string.IsNullOrWhiteSpace(program.Name)
                            ? $"Rotation {_rotationStates.Count + 1}"
                            : program.Name.Trim();

                        if (!_rotationStates.ContainsKey(name))
                        {
                            _rotationStates[name] = new RotationState(name, program.Slots.ToList());
                        }
                    }
                }

                if (schedule != null)
                {
                    foreach (var entry in schedule)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.RotationName))
                        {
                            continue;
                        }

                        if (!_rotationStates.ContainsKey(entry.RotationName))
                        {
                            continue;
                        }

                        if (!_scheduleByDay.TryGetValue(entry.Day, out var list))
                        {
                            list = new List<RotationScheduleEntry>();
                            _scheduleByDay[entry.Day] = list;
                        }

                        list.Add(entry);
                        list.Sort(static (a, b) => a.StartTime.CompareTo(b.StartTime));
                    }
                }

                _defaultRotationName = defaultRotationName?.Trim() ?? string.Empty;
            }
        }

        public ClockwheelSlot? NextSlot()
        {
            lock (_sync)
            {
                var rotationState = ResolveActiveRotationState();
                if (rotationState != null)
                {
                    var slot = rotationState.Slots[rotationState.Pointer];
                    rotationState.Pointer = (rotationState.Pointer + 1) % rotationState.Slots.Count;
                    return slot;
                }

                if (_slots.Count == 0)
                {
                    return null;
                }

                var queuedSlot = _slots[_pointer];
                _pointer = (_pointer + 1) % _slots.Count;
                return queuedSlot;
            }
        }

        public IReadOnlyList<ClockwheelSlot> GetUpcoming(int count)
        {
            if (count <= 0)
            {
                return Array.Empty<ClockwheelSlot>();
            }

            lock (_sync)
            {
                var rotationState = ResolveActiveRotationState();
                if (rotationState != null)
                {
                    var result = new List<ClockwheelSlot>(Math.Min(count, rotationState.Slots.Count));
                    for (var i = 0; i < count; i++)
                    {
                        var index = (rotationState.Pointer + i) % rotationState.Slots.Count;
                        result.Add(rotationState.Slots[index]);
                    }

                    return result;
                }

                if (_slots.Count == 0)
                {
                    return Array.Empty<ClockwheelSlot>();
                }

                var results = new List<ClockwheelSlot>(Math.Min(count, _slots.Count));
                for (var i = 0; i < count; i++)
                {
                    var index = (_pointer + i) % _slots.Count;
                    results.Add(_slots[index]);
                }

                return results;
            }
        }

        private RotationState? ResolveActiveRotationState()
        {
            if (_rotationStates.Count == 0)
            {
                return null;
            }

            var now = DateTimeOffset.Now;
            var rotationName = ResolveRotationName(now);
            if (!string.IsNullOrWhiteSpace(rotationName) && _rotationStates.TryGetValue(rotationName, out var scheduled))
            {
                if (scheduled.Slots.Count > 0)
                {
                    return scheduled;
                }
            }

            if (!string.IsNullOrWhiteSpace(_defaultRotationName) && _rotationStates.TryGetValue(_defaultRotationName, out var fallback) && fallback.Slots.Count > 0)
            {
                return fallback;
            }

            return _rotationStates.Values.FirstOrDefault(static state => state.Slots.Count > 0);
        }

        private string? ResolveRotationName(DateTimeOffset reference)
        {
            if (_scheduleByDay.Count == 0)
            {
                return null;
            }

            for (var offset = 0; offset < 7; offset++)
            {
                var probe = reference.AddDays(-offset);
                if (!_scheduleByDay.TryGetValue(probe.DayOfWeek, out var entries) || entries.Count == 0)
                {
                    continue;
                }

                if (offset == 0)
                {
                    for (var i = entries.Count - 1; i >= 0; i--)
                    {
                        if (entries[i].StartTime <= probe.TimeOfDay)
                        {
                            return entries[i].RotationName;
                        }
                    }
                }
                else
                {
                    return entries[^1].RotationName;
                }
            }

            return null;
        }

        private sealed class RotationState
        {
            public RotationState(string name, IReadOnlyList<ClockwheelSlot> slots)
            {
                Name = name;
                Slots = slots?.Where(static slot => slot != null).Select(static slot => slot!).ToList() ?? new List<ClockwheelSlot>();
            }

            public string Name { get; }
            public List<ClockwheelSlot> Slots { get; }
            public int Pointer { get; set; }
        }
    }
}
