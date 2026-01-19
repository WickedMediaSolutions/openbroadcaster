using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenBroadcaster.Core.Models
{
    /// <summary>
    /// Selection mode for TOH slot track selection.
    /// </summary>
    public enum TohSelectionMode
    {
        Sequential = 0,
        Random = 1
    }

    /// <summary>
    /// A single slot in the Top-of-the-Hour sequence.
    /// </summary>
    public sealed class TohSlot
    {
        public int SlotOrder { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int TrackCount { get; set; } = 1;
        public TohSelectionMode SelectionMode { get; set; } = TohSelectionMode.Random;
        public bool PreventRepeat { get; set; } = true;

        public TohSlot Clone()
        {
            return new TohSlot
            {
                SlotOrder = SlotOrder,
                CategoryId = CategoryId,
                CategoryName = CategoryName,
                TrackCount = TrackCount,
                SelectionMode = SelectionMode,
                PreventRepeat = PreventRepeat
            };
        }
    }

    /// <summary>
    /// Top-of-the-Hour settings, persisted as part of AppSettings.
    /// </summary>
    public sealed class TohSettings
    {
        /// <summary>
        /// Master enable/disable for TOH injection.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Offset in seconds from the top of the hour to fire (0 = exactly at :00:00).
        /// </summary>
        public int FireSecondOffset { get; set; }

        /// <summary>
        /// Allow TOH to fire when AutoDJ is running.
        /// </summary>
        public bool AllowDuringAutoDj { get; set; } = true;

        /// <summary>
        /// Allow TOH to fire during Live Assist (manual) mode.
        /// </summary>
        public bool AllowDuringLiveAssist { get; set; } = true;

        /// <summary>
        /// Ordered sequence of TOH slots.
        /// </summary>
        public ObservableCollection<TohSlot> Slots { get; set; } = new();

        /// <summary>
        /// Tracks the last hour TOH was fired to prevent double-fires.
        /// </summary>
        public int LastFiredHour { get; set; } = -1;

        /// <summary>
        /// Sequential index tracker per category for Sequential selection mode.
        /// Key = CategoryId.ToString(), Value = last used index.
        /// </summary>
        public ObservableCollection<TohSequentialIndex> SequentialIndices { get; set; } = new();

        public TohSettings Clone()
        {
            return new TohSettings
            {
                Enabled = Enabled,
                FireSecondOffset = FireSecondOffset,
                AllowDuringAutoDj = AllowDuringAutoDj,
                AllowDuringLiveAssist = AllowDuringLiveAssist,
                Slots = new ObservableCollection<TohSlot>((Slots ?? new()).Select(s => s?.Clone() ?? new TohSlot())),
                LastFiredHour = LastFiredHour,
                SequentialIndices = new ObservableCollection<TohSequentialIndex>((SequentialIndices ?? new()).Select(i => i?.Clone() ?? new TohSequentialIndex()))
            };
        }
    }

    /// <summary>
    /// Tracks sequential index for a category to support Sequential selection mode.
    /// </summary>
    public sealed class TohSequentialIndex
    {
        public string CategoryId { get; set; } = string.Empty;
        public int LastIndex { get; set; }

        public TohSequentialIndex Clone()
        {
            return new TohSequentialIndex
            {
                CategoryId = CategoryId,
                LastIndex = LastIndex
            };
        }
    }
}
