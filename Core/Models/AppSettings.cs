using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenBroadcaster.Core.Automation;

namespace OpenBroadcaster.Core.Models
{
    public sealed class AppSettings
    {
        public const string CurrentVersion = "1.1";

        public string Version { get; set; } = CurrentVersion;
        public AudioSettings Audio { get; set; } = new AudioSettings();
        public TwitchSettings Twitch { get; set; } = new TwitchSettings();
        public QueueSettings Queue { get; set; } = new QueueSettings();
        public EncoderSettings Encoder { get; set; } = new EncoderSettings();
        public CartWallSettings CartWall { get; set; } = new CartWallSettings();
        public OverlaySettings Overlay { get; set; } = new OverlaySettings();
        public AutomationSettings Automation { get; set; } = new AutomationSettings();
        public RequestSettings Requests { get; set; } = new RequestSettings();

        public void ApplyDefaults()
        {
            Version ??= CurrentVersion;
            Audio ??= new AudioSettings();
            Twitch ??= new TwitchSettings();
            Queue ??= new QueueSettings();
            CartWall ??= new CartWallSettings();
            CartWall.Pads ??= new List<CartPadSettings>();
            Encoder ??= new EncoderSettings();
            Encoder.Profiles ??= new ObservableCollection<EncoderProfile>();
            Overlay ??= new OverlaySettings();
            Automation ??= new AutomationSettings();
            Automation.ClockwheelSlots ??= new ObservableCollection<ClockwheelSlotSettings>();
            Automation.Rotations ??= new ObservableCollection<RotationDefinitionSettings>();
            Automation.RotationSchedule ??= new ObservableCollection<RotationScheduleEntrySettings>();
            Automation.SimpleRotations ??= new ObservableCollection<OpenBroadcaster.Core.Automation.SimpleRotation>();
            Automation.SimpleSchedule ??= new ObservableCollection<OpenBroadcaster.Core.Automation.SimpleSchedulerEntry>();
            Automation.TopOfHour ??= new TohSettings();
            Automation.TopOfHour.Slots ??= new ObservableCollection<TohSlot>();
            Automation.TopOfHour.SequentialIndices ??= new ObservableCollection<TohSequentialIndex>();
            if ((Automation.Rotations == null || Automation.Rotations.Count == 0) && Automation.ClockwheelSlots.Count > 0)
            {
                Automation.Rotations ??= new ObservableCollection<RotationDefinitionSettings>();
                Automation.Rotations.Add(new RotationDefinitionSettings
                {
                    Name = "Legacy Rotation",
                    Slots = new ObservableCollection<ClockwheelSlotSettings>(Automation.ClockwheelSlots.Select(static slot => slot?.Clone() ?? new ClockwheelSlotSettings()))
                });
            }
            Requests ??= new RequestSettings();
        }
    }

    public sealed class AudioSettings
    {
        public int MasterVolumePercent { get; set; } = 85;
        public int DeckADeviceId { get; set; } = -1;
        public int DeckBDeviceId { get; set; } = -1;
        public int DeckAVolumePercent { get; set; } = 100;
        public int DeckBVolumePercent { get; set; } = 100;
        public int CartWallDeviceId { get; set; } = -1;
        public int CartWallVolumePercent { get; set; } = 100;
        public int MicVolumePercent { get; set; } = 100;
        public int EncoderDeviceId { get; set; } = -1;
        public int MicInputDeviceId { get; set; } = -1;
    }

    public sealed class QueueSettings
    {
        public bool AutoAdvance { get; set; } = true;
        public int MaxHistoryItems { get; set; } = 100;
        public string DefaultSourceLabel { get; set; } = "Studio";
    }

    public sealed class CartWallSettings
    {
        public List<CartPadSettings> Pads { get; set; } = new();
    }

    public sealed class CartPadSettings
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#FF151C29";
        public string Hotkey { get; set; } = string.Empty;
        public bool LoopEnabled { get; set; }
    }

    public sealed class EncoderSettings
    {
        public bool AutoStart { get; set; }
        public ObservableCollection<EncoderProfile> Profiles { get; set; } = new();
    }

    public sealed class OverlaySettings
    {
        public bool Enabled { get; set; }
        public int Port { get; set; } = 9750;
        public int RequestListLimit { get; set; } = 5;
        public int RecentListLimit { get; set; } = 5;
        public string ArtworkFallbackUrl { get; set; } = "/assets/artwork-placeholder.svg";
        public string ArtworkFallbackFilePath { get; set; } = string.Empty;

        public OverlaySettings Clone()
        {
            return new OverlaySettings
            {
                Enabled = Enabled,
                Port = Port,
                RequestListLimit = RequestListLimit,
                RecentListLimit = RecentListLimit,
                ArtworkFallbackUrl = ArtworkFallbackUrl,
                ArtworkFallbackFilePath = ArtworkFallbackFilePath
            };
        }
    }

    public sealed class AutomationSettings
    {
        public bool AutoStartAutoDj { get; set; }
        public int TargetQueueDepth { get; set; } = 5;
        public ObservableCollection<ClockwheelSlotSettings> ClockwheelSlots { get; set; } = new();
        public ObservableCollection<RotationDefinitionSettings> Rotations { get; set; } = new();
        public ObservableCollection<RotationScheduleEntrySettings> RotationSchedule { get; set; } = new();
        public string DefaultRotationName { get; set; } = string.Empty;

        // New simple AutoDJ persistence
        public ObservableCollection<OpenBroadcaster.Core.Automation.SimpleRotation> SimpleRotations { get; set; } = new();
        public ObservableCollection<OpenBroadcaster.Core.Automation.SimpleSchedulerEntry> SimpleSchedule { get; set; } = new();

        // Top-of-the-Hour settings
        public TohSettings TopOfHour { get; set; } = new();

        public AutomationSettings Clone()
        {
            return new AutomationSettings
            {
                AutoStartAutoDj = AutoStartAutoDj,
                TargetQueueDepth = TargetQueueDepth,
                ClockwheelSlots = new ObservableCollection<ClockwheelSlotSettings>((ClockwheelSlots ?? new()).Select(static slot => slot?.Clone() ?? new ClockwheelSlotSettings())),
                Rotations = new ObservableCollection<RotationDefinitionSettings>((Rotations ?? new()).Select(static rotation => rotation?.Clone() ?? new RotationDefinitionSettings())),
                RotationSchedule = new ObservableCollection<RotationScheduleEntrySettings>((RotationSchedule ?? new()).Select(static entry => entry?.Clone() ?? new RotationScheduleEntrySettings())),
                DefaultRotationName = DefaultRotationName,
                SimpleRotations = new ObservableCollection<OpenBroadcaster.Core.Automation.SimpleRotation>(SimpleRotations ?? new()),
                SimpleSchedule = new ObservableCollection<OpenBroadcaster.Core.Automation.SimpleSchedulerEntry>(SimpleSchedule ?? new()),
                TopOfHour = TopOfHour?.Clone() ?? new TohSettings()
            };
        }
    }

    public sealed class RotationDefinitionSettings
    {
        public string Name { get; set; } = "New Rotation";
        public ObservableCollection<ClockwheelSlotSettings> Slots { get; set; } = new();

        public RotationDefinitionSettings Clone()
        {
            return new RotationDefinitionSettings
            {
                Name = Name,
                Slots = new ObservableCollection<ClockwheelSlotSettings>((Slots ?? new()).Select(static slot => slot?.Clone() ?? new ClockwheelSlotSettings()))
            };
        }
    }

    public sealed class RotationScheduleEntrySettings
    {
        public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
        public string StartTime { get; set; } = "00:00";
        public string RotationName { get; set; } = string.Empty;

        public RotationScheduleEntrySettings Clone()
        {
            return new RotationScheduleEntrySettings
            {
                Day = Day,
                StartTime = StartTime,
                RotationName = RotationName
            };
        }
    }

    public sealed class ClockwheelSlotSettings
    {
        public string CategoryName { get; set; } = string.Empty;
        public string TrackId { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;

        public ClockwheelSlot ToSlot()
        {
            var category = CategoryName?.Trim() ?? string.Empty;
            Guid? track = null;
            if (Guid.TryParse(TrackId, out var parsed) && parsed != Guid.Empty)
            {
                track = parsed;
            }

            return new ClockwheelSlot(category, track, DisplayLabel);
        }

        public ClockwheelSlotSettings Clone()
        {
            return new ClockwheelSlotSettings
            {
                CategoryName = CategoryName,
                TrackId = TrackId,
                DisplayLabel = DisplayLabel
            };
        }
    }

    public sealed class RequestSettings
    {
        public int MaxPendingRequests { get; set; } = 25;
        public int MaxRequestsPerUser { get; set; } = 3;
        public string SourceLabel { get; set; } = "Twitch Chat";

        public RequestSettings Clone()
        {
            return new RequestSettings
            {
                MaxPendingRequests = MaxPendingRequests,
                MaxRequestsPerUser = MaxRequestsPerUser,
                SourceLabel = SourceLabel
            };
        }
    }
}
