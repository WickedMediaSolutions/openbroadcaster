using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

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
    public sealed class TohSlot : INotifyPropertyChanged
    {
        // Hardcoded TOH category GUIDs - must match LibraryService
        public static readonly Guid StationIdsGuid = new("10000000-0000-0000-0000-000000000001");
        public static readonly Guid CommercialsGuid = new("10000000-0000-0000-0000-000000000002");
        public static readonly Guid JinglesGuid = new("10000000-0000-0000-0000-000000000003");

        private int _slotOrder;
        private Guid _categoryId = StationIdsGuid;
        private string _categoryName = "Station IDs";
        private int _trackCount = 1;
        private TohSelectionMode _selectionMode = TohSelectionMode.Random;
        private bool _preventRepeat = true;

        public int SlotOrder
        {
            get => _slotOrder;
            set => SetProperty(ref _slotOrder, value);
        }

        public Guid CategoryId
        {
            get => _categoryId;
            set
            {
                if (SetProperty(ref _categoryId, value))
                {
                    // Also update CategoryName and notify CategoryIndex changed
                    _categoryName = GetNameFromId(value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryName)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryIndex)));
                }
            }
        }

        public string CategoryName
        {
            get => _categoryName;
            set => SetProperty(ref _categoryName, value);
        }

        /// <summary>
        /// Index for ComboBox binding: 0=Station IDs, 1=Commercials, 2=Jingles
        /// </summary>
        [JsonIgnore]
        public int CategoryIndex
        {
            get
            {
                if (_categoryId == StationIdsGuid) return 0;
                if (_categoryId == CommercialsGuid) return 1;
                if (_categoryId == JinglesGuid) return 2;
                return 0;
            }
            set
            {
                var (id, name) = value switch
                {
                    1 => (CommercialsGuid, "Commercials"),
                    2 => (JinglesGuid, "Jingles"),
                    _ => (StationIdsGuid, "Station IDs")
                };
                
                if (_categoryId != id)
                {
                    _categoryId = id;
                    _categoryName = name;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryId)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryName)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryIndex)));
                }
            }
        }

        public int TrackCount
        {
            get => _trackCount;
            set => SetProperty(ref _trackCount, value);
        }

        public TohSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => SetProperty(ref _selectionMode, value);
        }

        public bool PreventRepeat
        {
            get => _preventRepeat;
            set => SetProperty(ref _preventRepeat, value);
        }

        private static string GetNameFromId(Guid id)
        {
            if (id == CommercialsGuid) return "Commercials";
            if (id == JinglesGuid) return "Jingles";
            return "Station IDs";
        }

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

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
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
