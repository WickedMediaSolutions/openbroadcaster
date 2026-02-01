using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Services;
using System.Collections.Generic;
using System.Linq;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Audio;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public event EventHandler<OpenBroadcaster.Core.Models.AppSettings>? SettingsChanged;
        private readonly AppSettingsStore _store;
        private readonly AudioService _audioService;
        private readonly AutoDjSettingsService _autoDjSettingsService;
        private readonly LibraryService? _libraryService;

        // Dialog invokers (overrideable for tests) - asynchronous for Avalonia dialogs
        public Func<SimpleRotation, List<string>, IEnumerable<string>, System.Threading.Tasks.Task<bool?>>? RotationDialogInvoker { get; set; }
        public Func<SimpleSchedulerEntry, List<SimpleRotation>, System.Threading.Tasks.Task<bool?>>? SchedulerDialogInvoker { get; set; }

        public SettingsViewModel(AppSettings settings, AudioService audioService, AppSettingsStore? store = null, LibraryService? libraryService = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _store = store ?? new AppSettingsStore();
            _autoDjSettingsService = new AutoDjSettingsService(loadFromDisk: true);
            _libraryService = libraryService;

            PlaybackDeviceOptions = new ObservableCollection<AudioDeviceInfo>(_audioService.GetOutputDevices());
            InputDeviceOptions = new ObservableCollection<AudioDeviceInfo>(_audioService.GetInputDevices());
            // Ensure encoder profile collection is initialized
            Settings.Encoder ??= new EncoderSettings();
            Settings.Encoder.Profiles ??= new System.Collections.ObjectModel.ObservableCollection<EncoderProfile>();
            EncoderProfiles = Settings.Encoder.Profiles;

            // Ensure Automation lists exist
            Settings.Automation ??= new OpenBroadcaster.Core.Models.AutomationSettings();
            Settings.Automation.SimpleRotations ??= new System.Collections.ObjectModel.ObservableCollection<SimpleRotation>();
            Settings.Automation.SimpleSchedule ??= new System.Collections.ObjectModel.ObservableCollection<SimpleSchedulerEntry>();

            // Build category options from library
            RotationCategoryOptions = BuildCategoryOptions(_libraryService?.GetCategories());

            // Load persisted AutoDJ rotations/schedule/default into in-memory settings
            SyncFromAutoDjSettings();

            AddEncoderCommand = new RelayCommand(_ => AddEncoderProfile());
            RemoveEncoderCommand = new RelayCommand(_ => RemoveSelectedEncoder(), _ => SelectedEncoder != null);

            // Simple AutoDJ commands (async dialog flows)
            AddSimpleRotationCommand = new AsyncRelayCommand(async _ => await AddSimpleRotationAsync());
            RemoveSimpleRotationCommand = new RelayCommand(_ => RemoveSelectedSimpleRotation(), _ => SelectedSimpleRotation != null);
            EditSimpleRotationCommand = new AsyncRelayCommand(async _ => await EditSimpleRotationAsync(), _ => SelectedSimpleRotation != null);
            SetDefaultSimpleRotationCommand = new RelayCommand(_ => SetDefaultSimpleRotation(), _ => SelectedSimpleRotation != null);
            AddSimpleScheduleEntryCommand = new AsyncRelayCommand(async _ => await AddSimpleScheduleEntryAsync());
            RemoveSimpleScheduleEntryCommand = new RelayCommand(_ => RemoveSelectedSimpleScheduleEntry(), _ => SelectedSimpleScheduleEntry != null);
            EditSimpleScheduleEntryCommand = new AsyncRelayCommand(async _ => await EditSimpleScheduleEntryAsync(), _ => SelectedSimpleScheduleEntry != null);
        }

        public AppSettings Settings { get; }

        public ObservableCollection<AudioDeviceInfo> PlaybackDeviceOptions { get; }
        public ObservableCollection<AudioDeviceInfo> InputDeviceOptions { get; }

        public IReadOnlyList<string> RotationCategoryOptions { get; }

        public AudioDeviceInfo? SelectedDeckADevice
        {
            get => PlaybackDeviceOptions.FirstOrDefault(d => d.DeviceNumber == Settings.Audio.DeckADeviceId);
            set
            {
                var id = value?.DeviceNumber ?? -1;
                if (Settings.Audio.DeckADeviceId != id)
                {
                    Settings.Audio.DeckADeviceId = id;
                    OnPropertyChanged();
                }
            }
        }

        public AudioDeviceInfo? SelectedDeckBDevice
        {
            get => PlaybackDeviceOptions.FirstOrDefault(d => d.DeviceNumber == Settings.Audio.DeckBDeviceId);
            set
            {
                var id = value?.DeviceNumber ?? -1;
                if (Settings.Audio.DeckBDeviceId != id)
                {
                    Settings.Audio.DeckBDeviceId = id;
                    OnPropertyChanged();
                }
            }
        }

        public AudioDeviceInfo? SelectedCartWallDevice
        {
            get => PlaybackDeviceOptions.FirstOrDefault(d => d.DeviceNumber == Settings.Audio.CartWallDeviceId);
            set
            {
                var id = value?.DeviceNumber ?? -1;
                if (Settings.Audio.CartWallDeviceId != id)
                {
                    Settings.Audio.CartWallDeviceId = id;
                    OnPropertyChanged();
                }
            }
        }

        public AudioDeviceInfo? SelectedEncoderDevice
        {
            get => PlaybackDeviceOptions.FirstOrDefault(d => d.DeviceNumber == Settings.Audio.EncoderDeviceId);
            set
            {
                var id = value?.DeviceNumber ?? -1;
                if (Settings.Audio.EncoderDeviceId != id)
                {
                    Settings.Audio.EncoderDeviceId = id;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Represents the single main output device selected in the simplified audio settings.
        /// Changing this will update Deck A, Deck B, Cart Wall and Encoder device ids so the
        /// application uses a single shared output device for all program audio.
        /// </summary>
        public AudioDeviceInfo? SelectedMainOutputDevice
        {
            get => PlaybackDeviceOptions.FirstOrDefault(d => d.DeviceNumber == Settings.Audio.DeckADeviceId);
            set
            {
                var id = value?.DeviceNumber ?? -1;
                if (Settings.Audio.DeckADeviceId != id || Settings.Audio.DeckBDeviceId != id || Settings.Audio.CartWallDeviceId != id || Settings.Audio.EncoderDeviceId != id)
                {
                    Settings.Audio.DeckADeviceId = id;
                    Settings.Audio.DeckBDeviceId = id;
                    Settings.Audio.CartWallDeviceId = id;
                    Settings.Audio.EncoderDeviceId = id;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedDeckADevice));
                    OnPropertyChanged(nameof(SelectedDeckBDevice));
                    OnPropertyChanged(nameof(SelectedCartWallDevice));
                    OnPropertyChanged(nameof(SelectedEncoderDevice));
                }
            }
        }

        public AudioDeviceInfo? SelectedMicInputDevice
        {
            get => InputDeviceOptions.FirstOrDefault(d => d.DeviceNumber == Settings.Audio.MicInputDeviceId);
            set
            {
                var id = value?.DeviceNumber ?? -1;
                if (Settings.Audio.MicInputDeviceId != id)
                {
                    Settings.Audio.MicInputDeviceId = id;
                    OnPropertyChanged();
                }
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<EncoderProfile> EncoderProfiles { get; private set; } = new();

        // Simple AutoDJ properties
        private SimpleRotation? _selectedSimpleRotation;
        public SimpleRotation? SelectedSimpleRotation
        {
            get => _selectedSimpleRotation;
            set
            {
                if (!ReferenceEquals(_selectedSimpleRotation, value))
                {
                    _selectedSimpleRotation = value;
                    OnPropertyChanged();
                }
            }
        }

        private SimpleSchedulerEntry? _selectedSimpleScheduleEntry;
        public SimpleSchedulerEntry? SelectedSimpleScheduleEntry
        {
            get => _selectedSimpleScheduleEntry;
            set
            {
                if (!ReferenceEquals(_selectedSimpleScheduleEntry, value))
                {
                    _selectedSimpleScheduleEntry = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand AddSimpleRotationCommand { get; private set; }
        public ICommand RemoveSimpleRotationCommand { get; private set; }
        public ICommand EditSimpleRotationCommand { get; private set; }
        public ICommand SetDefaultSimpleRotationCommand { get; private set; }
        public ICommand AddSimpleScheduleEntryCommand { get; private set; }
        public ICommand RemoveSimpleScheduleEntryCommand { get; private set; }
        public ICommand EditSimpleScheduleEntryCommand { get; private set; }

        private EncoderProfile? _selectedEncoder;
        public EncoderProfile? SelectedEncoder
        {
            get => _selectedEncoder;
            set
            {
                if (!ReferenceEquals(_selectedEncoder, value))
                {
                    _selectedEncoder = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand AddEncoderCommand { get; }
        public ICommand RemoveEncoderCommand { get; }

        private void AddEncoderProfile()
        {
            var p = new EncoderProfile { Name = "New Encoder", Host = "localhost" };
            EncoderProfiles.Add(p);
            SelectedEncoder = p;
            OnPropertyChanged(nameof(EncoderProfiles));
        }

        private void RemoveSelectedEncoder()
        {
            if (SelectedEncoder != null)
            {
                EncoderProfiles.Remove(SelectedEncoder);
                SelectedEncoder = null;
                OnPropertyChanged(nameof(EncoderProfiles));
            }
        }

        public void Save()
        {
            try
            {
                _store.Save(Settings);
                try
                {
                    _audioService.ApplyAudioSettings(Settings.Audio);
                }
                catch { }
                try
                {
                    SettingsChanged?.Invoke(this, Settings);
                    // Persist AutoDJ rotations/schedule/default
                    SyncToAutoDjSettings();
                }
                catch { }
            }
            catch { }
        }

        // ----- Simple AutoDJ helpers (ported from WPF SettingsViewModel) -----
        private async System.Threading.Tasks.Task AddSimpleRotationAsync()
        {
            var list = Settings?.Automation?.SimpleRotations;
            if (list == null) return;
            var rot = new SimpleRotation { Name = $"Rotation {list.Count + 1}", Enabled = true, CategoryNames = new List<string>() };
            var existingNames = list.Select(r => r.Name);
            var categoryOptions = RotationCategoryOptions.ToList();
            var result = await (RotationDialogInvoker?.Invoke(rot, categoryOptions, existingNames)
                ?? new OpenBroadcaster.Avalonia.Views.RotationDialog(rot, categoryOptions, existingNames).ShowDialog<bool?>(null));
            if (result == true)
            {
                if (rot.IsActive)
                {
                    foreach (var r in list)
                    {
                        r.IsActive = false;
                    }
                }
                list.Add(rot);
                SelectedSimpleRotation = rot;
                if (Settings?.Automation != null && string.IsNullOrWhiteSpace(Settings.Automation.DefaultRotationName))
                {
                    Settings.Automation.DefaultRotationName = rot.Name;
                    _autoDjSettingsService.DefaultRotationId = rot.Id;
                    _autoDjSettingsService.DefaultRotationName = rot.Name;
                }
                OnPropertyChanged(nameof(Settings));
            }
        }

        private async System.Threading.Tasks.Task EditSimpleRotationAsync()
        {
            var list = Settings?.Automation?.SimpleRotations;
            if (list == null || SelectedSimpleRotation == null) return;

            var working = CloneSimpleRotation(SelectedSimpleRotation);
            var existingNames = list.Where(r => r.Id != SelectedSimpleRotation.Id).Select(r => r.Name);
            var categoryOptions = RotationCategoryOptions.ToList();
            var result = await (RotationDialogInvoker?.Invoke(working, categoryOptions, existingNames)
                ?? new OpenBroadcaster.Avalonia.Views.RotationDialog(working, categoryOptions, existingNames).ShowDialog<bool?>(null));
            if (result == true)
            {
                if (working.IsActive && !SelectedSimpleRotation.IsActive)
                {
                    foreach (var r in list.Where(r => r.Id != SelectedSimpleRotation.Id))
                    {
                        r.IsActive = false;
                    }
                }
                CopySimpleRotation(SelectedSimpleRotation, working);
                OnPropertyChanged(nameof(Settings));
            }
        }

        private void RemoveSelectedSimpleRotation()
        {
            var list = Settings?.Automation?.SimpleRotations;
            if (list == null || SelectedSimpleRotation == null) return;
            if (list.Remove(SelectedSimpleRotation))
            {
                SelectedSimpleRotation = list.FirstOrDefault();
                OnPropertyChanged(nameof(Settings));
            }
        }

        private async System.Threading.Tasks.Task AddSimpleScheduleEntryAsync()
        {
            var list = Settings?.Automation?.SimpleSchedule;
            var rotations = Settings?.Automation?.SimpleRotations;
            if (list == null || rotations == null || rotations.Count == 0) return;

            var entry = new SimpleSchedulerEntry
            {
                RotationId = rotations.FirstOrDefault()?.Id ?? Guid.Empty,
                RotationName = rotations.FirstOrDefault()?.Name ?? string.Empty,
                Enabled = true,
                StartTime = TimeSpan.Zero,
                EndTime = TimeSpan.FromHours(24)
            };

            var rotationList = rotations.ToList();
            var result = await (SchedulerDialogInvoker?.Invoke(entry, rotationList)
                ?? new OpenBroadcaster.Avalonia.Views.SchedulerDialog(entry, rotationList).ShowDialog<bool?>(null));
            if (result == true)
            {
                list.Add(entry);
                SelectedSimpleScheduleEntry = entry;
                OnPropertyChanged(nameof(Settings));
            }
        }

        private void RemoveSelectedSimpleScheduleEntry()
        {
            var list = Settings?.Automation?.SimpleSchedule;
            if (list == null || SelectedSimpleScheduleEntry == null) return;
            if (list.Remove(SelectedSimpleScheduleEntry))
            {
                SelectedSimpleScheduleEntry = list.FirstOrDefault();
                OnPropertyChanged(nameof(Settings));
            }
        }

        private async System.Threading.Tasks.Task EditSimpleScheduleEntryAsync()
        {
            var list = Settings?.Automation?.SimpleSchedule;
            var rotations = Settings?.Automation?.SimpleRotations;
            if (list == null || rotations == null || rotations.Count == 0 || SelectedSimpleScheduleEntry == null) return;

            var working = CloneSimpleScheduleEntry(SelectedSimpleScheduleEntry);
            var rotationList = rotations.ToList();
            var result = await (SchedulerDialogInvoker?.Invoke(working, rotationList)
                ?? new OpenBroadcaster.Avalonia.Views.SchedulerDialog(working, rotationList).ShowDialog<bool?>(null));
            if (result == true)
            {
                CopySimpleScheduleEntry(SelectedSimpleScheduleEntry, working);
                OnPropertyChanged(nameof(Settings));
            }
        }

        private void SetDefaultSimpleRotation()
        {
            if (Settings?.Automation == null || SelectedSimpleRotation == null) return;
            Settings.Automation.DefaultRotationName = SelectedSimpleRotation.Name ?? string.Empty;
            _autoDjSettingsService.DefaultRotationName = Settings.Automation.DefaultRotationName;
            _autoDjSettingsService.DefaultRotationId = SelectedSimpleRotation.Id;
            OnPropertyChanged(nameof(Settings));
        }

        private static SimpleRotation CloneSimpleRotation(SimpleRotation source)
        {
            return new SimpleRotation
            {
                Id = source.Id,
                Name = source.Name,
                Enabled = source.Enabled,
                IsActive = source.IsActive,
                SortOrder = source.SortOrder,
                Slots = new List<SimpleRotationSlot>((source.Slots ?? new()).Select(s => new SimpleRotationSlot { CategoryId = s.CategoryId, CategoryName = s.CategoryName, Weight = s.Weight })),
                CategoryIds = new List<string>(source.CategoryIds ?? new()),
                CategoryNames = new List<string>(source.CategoryNames ?? new())
            };
        }

        private static void CopySimpleRotation(SimpleRotation target, SimpleRotation source)
        {
            target.Name = source.Name;
            target.Enabled = source.Enabled;
            target.IsActive = source.IsActive;
            target.SortOrder = source.SortOrder;
            target.Slots = new List<SimpleRotationSlot>((source.Slots ?? new()).Select(s => new SimpleRotationSlot { CategoryId = s.CategoryId, CategoryName = s.CategoryName, Weight = s.Weight }));
            target.CategoryIds = new List<string>(source.CategoryIds ?? new());
            target.CategoryNames = new List<string>(source.CategoryNames ?? new());
        }

        private static SimpleSchedulerEntry CloneSimpleScheduleEntry(SimpleSchedulerEntry source)
        {
            return new SimpleSchedulerEntry
            {
                RotationId = source.RotationId,
                RotationName = source.RotationName,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                Enabled = source.Enabled,
                Day = source.Day
            };
        }

        private void CopySimpleScheduleEntry(SimpleSchedulerEntry target, SimpleSchedulerEntry source)
        {
            target.RotationName = source.RotationName;
            target.RotationId = source.RotationId;
            target.StartTime = source.StartTime;
            target.EndTime = source.EndTime;
            target.Enabled = source.Enabled;
            target.Day = source.Day;
        }

        private void SyncFromAutoDjSettings()
        {
            var autoRotations = new System.Collections.ObjectModel.ObservableCollection<SimpleRotation>(_autoDjSettingsService.Rotations.Select(CloneSimpleRotation));
            var autoSchedule = new System.Collections.ObjectModel.ObservableCollection<SimpleSchedulerEntry>(_autoDjSettingsService.Schedule.Select(CloneSimpleScheduleEntry));

            Settings.Automation.SimpleRotations = autoRotations;
            Settings.Automation.SimpleSchedule = autoSchedule;
            Settings.Automation.DefaultRotationName = _autoDjSettingsService.DefaultRotationName ?? string.Empty;
        }

        private void SyncToAutoDjSettings()
        {
            _autoDjSettingsService.Rotations = Settings?.Automation?.SimpleRotations?.Select(CloneSimpleRotation).ToList() ?? new List<SimpleRotation>();
            _autoDjSettingsService.Schedule = Settings?.Automation?.SimpleSchedule?.Select(CloneSimpleScheduleEntry).ToList() ?? new List<SimpleSchedulerEntry>();

            var defaultRot = _autoDjSettingsService.Rotations.FirstOrDefault(r => string.Equals(r.Name, Settings?.Automation?.DefaultRotationName, StringComparison.OrdinalIgnoreCase));
            _autoDjSettingsService.DefaultRotationId = defaultRot?.Id ?? Guid.Empty;
            _autoDjSettingsService.DefaultRotationName = defaultRot?.Name ?? Settings?.Automation?.DefaultRotationName ?? string.Empty;

            _autoDjSettingsService.SaveAll();
        }

        private static IReadOnlyList<string> BuildCategoryOptions(IReadOnlyCollection<LibraryCategory>? categories)
        {
            var options = new List<string> { "Library", "Uncategorized" };

            if (categories != null)
            {
                var ordered = categories
                    .Where(static category => category != null)
                    .Select(static category => category!.Name)
                    .Where(static name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase);

                options.AddRange(ordered);
            }

            return options;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
