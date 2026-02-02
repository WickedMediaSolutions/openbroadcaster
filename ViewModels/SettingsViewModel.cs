using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Views;

namespace OpenBroadcaster.ViewModels
{
    public sealed class SettingsViewModel : INotifyPropertyChanged
    {
        // Parameterless constructor for test compatibility
        public SettingsViewModel() : this(new AppSettings(), null, null, null, null) { }
        // Overloaded constructors for test compatibility
        public SettingsViewModel(AppSettings settings) : this(settings, null, null, null, null) { }
        public SettingsViewModel(AppSettings settings, IReadOnlyList<AudioDeviceInfo>? playbackDevices, IReadOnlyList<AudioDeviceInfo>? inputDevices) : this(settings, playbackDevices, inputDevices, null, null) { }
        public ICommand AddSimpleRotationCommand { get; }
        public ICommand RemoveSimpleRotationCommand { get; }
        public ICommand EditSimpleRotationCommand { get; }
        public ICommand SetDefaultSimpleRotationCommand { get; }
        public ICommand AddSimpleScheduleEntryCommand { get; }
        public ICommand RemoveSimpleScheduleEntryCommand { get; }
        public ICommand EditSimpleScheduleEntryCommand { get; }
        
        // Top-of-Hour commands
        public ICommand AddTohSlotCommand { get; }
        public ICommand RemoveTohSlotCommand { get; }
        public ICommand MoveTohSlotUpCommand { get; }
        public ICommand MoveTohSlotDownCommand { get; }
        
        private AppSettings _original;
        private AppSettings _working;
        private EncoderProfile? _selectedEncoderProfile;
        private RotationDefinitionSettings? _selectedRotation;
        private ClockwheelSlotSettings? _selectedClockwheelSlot;
        private RotationScheduleEntrySettings? _selectedScheduleEntry;
        private readonly AutoDjSettingsService _autoDjSettingsService;

        // Simple AutoDJ (minimal UI support)
        private SimpleRotation? _selectedSimpleRotation;
        private SimpleSchedulerEntry? _selectedSimpleScheduleEntry;
        
        // Top-of-Hour selection
        private TohSlot? _selectedTohSlot;
        private IReadOnlyList<LibraryCategory>? _libraryCategories;

        // UI dialog delegates (settable for tests/headless scenarios)
        public Func<SimpleRotation, List<string>, IEnumerable<string>, bool?>? RotationDialogInvoker { get; set; }
        public Func<SimpleSchedulerEntry, List<SimpleRotation>, bool?>? SchedulerDialogInvoker { get; set; }

        public SettingsViewModel(AppSettings settings,
            IReadOnlyList<AudioDeviceInfo>? playbackDevices = null,
            IReadOnlyList<AudioDeviceInfo>? inputDevices = null,
            IReadOnlyList<LibraryCategory>? libraryCategories = null,
            AutoDjSettingsService? autoDjSettingsService = null)
        {
            _autoDjSettingsService = autoDjSettingsService ?? new AutoDjSettingsService();

            _original = Clone(settings ?? new AppSettings());
            _working = Clone(settings ?? new AppSettings());
            _libraryCategories = libraryCategories;

            // Ensure Automation and its lists are always initialized
            _working.Automation ??= new AutomationSettings();
            _working.Automation.SimpleRotations ??= new ObservableCollection<SimpleRotation>();
            _working.Automation.SimpleSchedule ??= new ObservableCollection<SimpleSchedulerEntry>();

            // Load persisted AutoDJ data
            SyncFromAutoDjSettings();

            ApplyCommand = new RelayCommand(_ => Apply(), _ => IsDirty);
            CancelCommand = new RelayCommand(_ => Cancel(), _ => IsDirty);
            ReloadCommand = new RelayCommand(_ => Reload());
            AddEncoderProfileCommand = new RelayCommand(_ => AddEncoderProfile());
            RemoveEncoderProfileCommand = new RelayCommand(_ => RemoveSelectedEncoderProfile(), _ => CanRemoveEncoderProfile());
            AddRotationCommand = new RelayCommand(_ => AddRotation());
            RemoveRotationCommand = new RelayCommand(_ => RemoveSelectedRotation(), _ => CanRemoveRotation());
            AddClockwheelSlotCommand = new RelayCommand(_ => AddClockwheelSlot(), _ => SelectedRotation != null);
            RemoveClockwheelSlotCommand = new RelayCommand(_ => RemoveSelectedClockwheelSlot(), _ => CanRemoveClockwheelSlot());
            AddScheduleEntryCommand = new RelayCommand(_ => AddScheduleEntry());
            RemoveScheduleEntryCommand = new RelayCommand(_ => RemoveSelectedScheduleEntry(), _ => CanRemoveScheduleEntry());
            PlaybackDeviceOptions = BuildDeviceOptions(playbackDevices, "System Default");
            InputDeviceOptions = BuildDeviceOptions(inputDevices, "Default Microphone");
            RotationCategoryOptions = BuildCategoryOptions(libraryCategories);
            EncoderProtocolValues = Enum.GetValues<EncoderProtocol>();
            EncoderFormatValues = Enum.GetValues<EncoderFormat>();
            DayOfWeekValues = Enum.GetValues<DayOfWeek>();
            EnsureEncoderSelection();
            EnsureRotationSelection();

            // Simple AutoDJ commands
            AddSimpleRotationCommand = new RelayCommand(_ => AddSimpleRotation());
            RemoveSimpleRotationCommand = new RelayCommand(_ => RemoveSelectedSimpleRotation(), _ => SelectedSimpleRotation != null);
            EditSimpleRotationCommand = new RelayCommand(_ => EditSimpleRotation(), _ => SelectedSimpleRotation != null);
            SetDefaultSimpleRotationCommand = new RelayCommand(_ => SetDefaultSimpleRotation(), _ => SelectedSimpleRotation != null);
            AddSimpleScheduleEntryCommand = new RelayCommand(_ => AddSimpleScheduleEntry());
            RemoveSimpleScheduleEntryCommand = new RelayCommand(_ => RemoveSelectedSimpleScheduleEntry(), _ => SelectedSimpleScheduleEntry != null);
            EditSimpleScheduleEntryCommand = new RelayCommand(_ => EditSimpleScheduleEntry(), _ => SelectedSimpleScheduleEntry != null);
            
            // Top-of-Hour commands
            AddTohSlotCommand = new RelayCommand(_ => AddTohSlot());
            RemoveTohSlotCommand = new RelayCommand(_ => RemoveSelectedTohSlot(), _ => SelectedTohSlot != null);
            MoveTohSlotUpCommand = new RelayCommand(_ => MoveTohSlotUp(), _ => CanMoveTohSlotUp());
            MoveTohSlotDownCommand = new RelayCommand(_ => MoveTohSlotDown(), _ => CanMoveTohSlotDown());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<AppSettings>? SettingsChanged;

        public AppSettings Settings => _working;

        public bool IsDirty
        {
            get => !ReferenceEquals(_original, _working) && !Serialize(_original).Equals(Serialize(_working), StringComparison.Ordinal);
        }

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand AddEncoderProfileCommand { get; }
        public ICommand RemoveEncoderProfileCommand { get; }
        public ICommand AddRotationCommand { get; }
        public ICommand RemoveRotationCommand { get; }
        public ICommand AddClockwheelSlotCommand { get; }
        public ICommand RemoveClockwheelSlotCommand { get; }
        public ICommand AddScheduleEntryCommand { get; }
        public ICommand RemoveScheduleEntryCommand { get; }
        public IReadOnlyList<EncoderProtocol> EncoderProtocolValues { get; }
        public IReadOnlyList<EncoderFormat> EncoderFormatValues { get; }
        public IReadOnlyList<DayOfWeek> DayOfWeekValues { get; }
        public IReadOnlyList<AudioDeviceOption> PlaybackDeviceOptions { get; }
        public IReadOnlyList<AudioDeviceOption> InputDeviceOptions { get; }
        public IReadOnlyList<string> RotationCategoryOptions { get; }
        public EncoderProfile? SelectedEncoderProfile
        {
            get => _selectedEncoderProfile;
            set
            {
                if (!ReferenceEquals(_selectedEncoderProfile, value))
                {
                    _selectedEncoderProfile = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RotationDefinitionSettings? SelectedRotation
        {
            get => _selectedRotation;
            set
            {
                if (!ReferenceEquals(_selectedRotation, value))
                {
                    _selectedRotation = value;
                    OnPropertyChanged();
                    EnsureRotationSlotSelection();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ClockwheelSlotSettings? SelectedClockwheelSlot
        {
            get => _selectedClockwheelSlot;
            set
            {
                if (!ReferenceEquals(_selectedClockwheelSlot, value))
                {
                    _selectedClockwheelSlot = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RotationScheduleEntrySettings? SelectedScheduleEntry
        {
            get => _selectedScheduleEntry;
            set
            {
                if (!ReferenceEquals(_selectedScheduleEntry, value))
                {
                    _selectedScheduleEntry = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public SimpleRotation? SelectedSimpleRotation
        {
            get => _selectedSimpleRotation;
            set { if (!ReferenceEquals(_selectedSimpleRotation, value)) { _selectedSimpleRotation = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }
        public SimpleSchedulerEntry? SelectedSimpleScheduleEntry
        {
            get => _selectedSimpleScheduleEntry;
            set { if (!ReferenceEquals(_selectedSimpleScheduleEntry, value)) { _selectedSimpleScheduleEntry = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        public TohSlot? SelectedTohSlot
        {
            get => _selectedTohSlot;
            set
            {
                if (!ReferenceEquals(_selectedTohSlot, value))
                {
                    _selectedTohSlot = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Returns only the permanent TOH categories (Station IDs, Commercials, Jingles).
        /// </summary>
        public IReadOnlyList<TohCategoryOption> TohCategoryOptions
        {
            get
            {
                var options = new List<TohCategoryOption>();
                if (_libraryCategories != null)
                {
                    // Only include permanent TOH categories
                    foreach (var cat in _libraryCategories
                        .Where(c => LibraryService.IsTohCategory(c.Id))
                        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        options.Add(new TohCategoryOption(cat.Id, cat.Name));
                    }
                }
                return options;
            }
        }

        public void NotifySettingsModified()
        {
            OnPropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
        }

        public void Apply()
        {
            if (!IsDirty)
            {
                return;
            }

            _original = Clone(_working);
            OnPropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
            SettingsChanged?.Invoke(this, Clone(_original));

            // Persist AutoDJ rotations/schedule/default
            SyncToAutoDjSettings();
        }

        public void Cancel()
        {
            if (!IsDirty)
            {
                return;
            }

            _working = Clone(_original);
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
            EnsureEncoderSelection();
            EnsureRotationSelection();
        }

        public void Reload()
        {
            _working = Clone(_original);
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
            EnsureEncoderSelection();
            EnsureRotationSelection();
        }

        public void Update(AppSettings settings)
        {
            _original = Clone(settings ?? new AppSettings());
            _working = Clone(settings ?? new AppSettings());
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
            EnsureEncoderSelection();
            EnsureRotationSelection();
            SyncFromAutoDjSettings();
        }

        private void AddEncoderProfile()
        {
            var encoderSettings = Settings?.Encoder;
            if (encoderSettings == null)
            {
                return;
            }

            var profile = new EncoderProfile
            {
                Name = $"Encoder {encoderSettings.Profiles.Count + 1}"
            };

            encoderSettings.Profiles.Add(profile);
            SelectedEncoderProfile = profile;
            NotifySettingsModified();
        }

        private void RemoveSelectedEncoderProfile()
        {
            var encoderSettings = Settings?.Encoder;
            if (encoderSettings == null || SelectedEncoderProfile == null)
            {
                return;
            }

            if (encoderSettings.Profiles.Remove(SelectedEncoderProfile))
            {
                SelectedEncoderProfile = encoderSettings.Profiles.FirstOrDefault();
                NotifySettingsModified();
            }
        }

        private bool CanRemoveEncoderProfile()
        {
            return Settings?.Encoder?.Profiles?.Count > 0 && SelectedEncoderProfile != null;
        }

        private void AddClockwheelSlot()
        {
            var rotation = SelectedRotation;
            if (rotation == null)
            {
                return;
            }

            rotation.Slots ??= new ObservableCollection<ClockwheelSlotSettings>();
            var slot = new ClockwheelSlotSettings
            {
                DisplayLabel = $"Slot {rotation.Slots.Count + 1}",
                CategoryName = RotationCategoryOptions.FirstOrDefault() ?? string.Empty
            };

            rotation.Slots.Add(slot);
            SelectedClockwheelSlot = slot;
            NotifySettingsModified();
        }

        private void RemoveSelectedClockwheelSlot()
        {
            var slots = SelectedRotation?.Slots;
            if (slots == null || SelectedClockwheelSlot == null)
            {
                return;
            }

            if (slots.Remove(SelectedClockwheelSlot))
            {
                SelectedClockwheelSlot = slots.FirstOrDefault();
                NotifySettingsModified();
            }
        }

        private bool CanRemoveClockwheelSlot()
        {
            return SelectedRotation?.Slots?.Count > 0 && SelectedClockwheelSlot != null;
        }

        private void AddRotation()
        {
            var automation = Settings?.Automation;
            if (automation == null)
            {
                return;
            }

            automation.Rotations ??= new ObservableCollection<RotationDefinitionSettings>();
            var rotation = new RotationDefinitionSettings
            {
                Name = $"Rotation {automation.Rotations.Count + 1}"
            };

            automation.Rotations.Add(rotation);
            SelectedRotation = rotation;
            NotifySettingsModified();
        }

        private void RemoveSelectedRotation()
        {
            var rotations = Settings?.Automation?.Rotations;
            if (rotations == null || SelectedRotation == null)
            {
                return;
            }

            if (rotations.Remove(SelectedRotation))
            {
                SelectedRotation = rotations.FirstOrDefault();
                NotifySettingsModified();
            }
        }

        private bool CanRemoveRotation()
        {
            return Settings?.Automation?.Rotations?.Count > 0 && SelectedRotation != null;
        }

        private void AddScheduleEntry()
        {
            var automation = Settings?.Automation;
            if (automation == null)
            {
                return;
            }

            automation.RotationSchedule ??= new ObservableCollection<RotationScheduleEntrySettings>();
            var defaultRotationName = SelectedRotation?.Name
                ?? automation.Rotations?.FirstOrDefault()?.Name
                ?? string.Empty;

            var entry = new RotationScheduleEntrySettings
            {
                Day = DayOfWeek.Monday,
                StartTime = "00:00",
                RotationName = defaultRotationName
            };

            automation.RotationSchedule.Add(entry);
            SelectedScheduleEntry = entry;
            NotifySettingsModified();
        }

        private void RemoveSelectedScheduleEntry()
        {
            var schedule = Settings?.Automation?.RotationSchedule;
            if (schedule == null || SelectedScheduleEntry == null)
            {
                return;
            }

            if (schedule.Remove(SelectedScheduleEntry))
            {
                SelectedScheduleEntry = schedule.FirstOrDefault();
                NotifySettingsModified();
            }
        }

        private bool CanRemoveScheduleEntry()
        {
            return Settings?.Automation?.RotationSchedule?.Count > 0 && SelectedScheduleEntry != null;
        }

        private void AddSimpleRotation()
        {
            var list = Settings?.Automation?.SimpleRotations;
            if (list == null) return;
            var rot = new SimpleRotation { Name = $"Rotation {list.Count + 1}", Enabled = true, CategoryNames = new List<string>() };
            var existingNames = list.Select(r => r.Name);
            var categoryOptions = RotationCategoryOptions.ToList();
            var result = RotationDialogInvoker?.Invoke(rot, categoryOptions, existingNames)
                ?? new OpenBroadcaster.Views.RotationDialog(rot, categoryOptions, existingNames).ShowDialog();
            if (result == true)
            {
                // Ensure only one rotation can be active at a time
                if (rot.IsActive)
                {
                    foreach (var r in list)
                    {
                        r.IsActive = false;
                    }
                }
                list.Add(rot);
                SelectedSimpleRotation = rot;
                // If no default is set yet, make the first rotation the default
                if (Settings?.Automation != null && string.IsNullOrWhiteSpace(Settings.Automation.DefaultRotationName))
                {
                    Settings.Automation.DefaultRotationName = rot.Name;
                    _autoDjSettingsService.DefaultRotationId = rot.Id;
                    _autoDjSettingsService.DefaultRotationName = rot.Name;
                }
                NotifySettingsModified();
            }
        }

        private void EditSimpleRotation()
        {
            var list = Settings?.Automation?.SimpleRotations;
            if (list == null || SelectedSimpleRotation == null)
            {
                return;
            }

            var working = CloneSimpleRotation(SelectedSimpleRotation);
            var existingNames = list.Where(r => r.Id != SelectedSimpleRotation.Id).Select(r => r.Name);
            var categoryOptions = RotationCategoryOptions.ToList();
            var result = RotationDialogInvoker?.Invoke(working, categoryOptions, existingNames)
                ?? new OpenBroadcaster.Views.RotationDialog(working, categoryOptions, existingNames).ShowDialog();
            if (result == true)
            {
                // Ensure only one rotation can be active at a time
                if (working.IsActive && !SelectedSimpleRotation.IsActive)
                {
                    foreach (var r in list.Where(r => r.Id != SelectedSimpleRotation.Id))
                    {
                        r.IsActive = false;
                    }
                }
                CopySimpleRotation(SelectedSimpleRotation, working);
                NotifySettingsModified();
            }
        }
        private void RemoveSelectedSimpleRotation()
        {
            var list = Settings?.Automation?.SimpleRotations;
            if (list == null || SelectedSimpleRotation == null) return;
            if (list.Remove(SelectedSimpleRotation))
            {
                SelectedSimpleRotation = list.FirstOrDefault();
                NotifySettingsModified();
            }
        }
        private void AddSimpleScheduleEntry()
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
            var result = SchedulerDialogInvoker?.Invoke(entry, rotationList)
                ?? new OpenBroadcaster.Views.SchedulerDialog(entry, rotationList).ShowDialog();
            if (result == true)
            {
                list.Add(entry);
                SelectedSimpleScheduleEntry = entry;
                NotifySettingsModified();
            }
        }
        private void RemoveSelectedSimpleScheduleEntry()
        {
            var list = Settings?.Automation?.SimpleSchedule;
            if (list == null || SelectedSimpleScheduleEntry == null) return;
            if (list.Remove(SelectedSimpleScheduleEntry))
            {
                SelectedSimpleScheduleEntry = list.FirstOrDefault();
                NotifySettingsModified();
            }
        }

        private void EditSimpleScheduleEntry()
        {
            var list = Settings?.Automation?.SimpleSchedule;
            var rotations = Settings?.Automation?.SimpleRotations;
            if (list == null || rotations == null || rotations.Count == 0 || SelectedSimpleScheduleEntry == null)
            {
                return;
            }

            var working = CloneSimpleScheduleEntry(SelectedSimpleScheduleEntry);
            var rotationList = rotations.ToList();
            var result = SchedulerDialogInvoker?.Invoke(working, rotationList)
                ?? new OpenBroadcaster.Views.SchedulerDialog(working, rotationList).ShowDialog();
            if (result == true)
            {
                CopySimpleScheduleEntry(SelectedSimpleScheduleEntry, working);
                NotifySettingsModified();
            }
        }

        private void SetDefaultSimpleRotation()
        {
            if (Settings?.Automation == null || SelectedSimpleRotation == null)
            {
                return;
            }

            Settings.Automation.DefaultRotationName = SelectedSimpleRotation.Name ?? string.Empty;
            _autoDjSettingsService.DefaultRotationName = Settings.Automation.DefaultRotationName;
            _autoDjSettingsService.DefaultRotationId = SelectedSimpleRotation.Id;
            NotifySettingsModified();
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

        private static void CopySimpleScheduleEntry(SimpleSchedulerEntry target, SimpleSchedulerEntry source)
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
            // Pull persisted rotations/schedule/default into working settings
            var autoRotations = CloneSimpleRotationsCollection(_autoDjSettingsService.Rotations ?? new());
            var autoSchedule = CloneSimpleScheduleCollection(_autoDjSettingsService.Schedule ?? new());

            _working.Automation.SimpleRotations = autoRotations;
            _working.Automation.SimpleSchedule = autoSchedule;
            _working.Automation.DefaultRotationName = _autoDjSettingsService.DefaultRotationName ?? string.Empty;
        }

        private void SyncToAutoDjSettings()
        {
            _autoDjSettingsService.Rotations = CloneSimpleRotationsList(Settings?.Automation?.SimpleRotations ?? new ObservableCollection<SimpleRotation>());
            _autoDjSettingsService.Schedule = CloneSimpleScheduleList(Settings?.Automation?.SimpleSchedule ?? new ObservableCollection<SimpleSchedulerEntry>());

            var defaultRot = _autoDjSettingsService.Rotations.FirstOrDefault(r => string.Equals(r.Name, Settings?.Automation?.DefaultRotationName, StringComparison.OrdinalIgnoreCase));
            _autoDjSettingsService.DefaultRotationId = defaultRot?.Id ?? Guid.Empty;
            _autoDjSettingsService.DefaultRotationName = defaultRot?.Name ?? Settings?.Automation?.DefaultRotationName ?? string.Empty;

            _autoDjSettingsService.SaveAll();
        }

        private static ObservableCollection<SimpleRotation> CloneSimpleRotationsCollection(IEnumerable<SimpleRotation> source)
        {
            return new ObservableCollection<SimpleRotation>(source.Select(CloneSimpleRotation));
        }

        private static ObservableCollection<SimpleSchedulerEntry> CloneSimpleScheduleCollection(IEnumerable<SimpleSchedulerEntry> source)
        {
            return new ObservableCollection<SimpleSchedulerEntry>(source.Select(CloneSimpleScheduleEntry));
        }

        private static List<SimpleRotation> CloneSimpleRotationsList(IEnumerable<SimpleRotation> source)
        {
            return source.Select(CloneSimpleRotation).ToList();
        }

        private static List<SimpleSchedulerEntry> CloneSimpleScheduleList(IEnumerable<SimpleSchedulerEntry> source)
        {
            return source.Select(CloneSimpleScheduleEntry).ToList();
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

        private void EnsureEncoderSelection()
        {
            var profiles = Settings?.Encoder?.Profiles;
            if (profiles == null || profiles.Count == 0)
            {
                SelectedEncoderProfile = null;
                return;
            }

            if (SelectedEncoderProfile == null || !profiles.Contains(SelectedEncoderProfile))
            {
                SelectedEncoderProfile = profiles.FirstOrDefault();
            }
        }

        private void EnsureRotationSelection()
        {
            var rotations = Settings?.Automation?.Rotations;
            if (rotations == null || rotations.Count == 0)
            {
                SelectedRotation = null;
                SelectedClockwheelSlot = null;
            }
            else if (SelectedRotation == null || !rotations.Contains(SelectedRotation))
            {
                SelectedRotation = rotations.FirstOrDefault();
            }

            EnsureRotationSlotSelection();
            EnsureScheduleSelection();
        }

        private void EnsureRotationSlotSelection()
        {
            var slots = SelectedRotation?.Slots;
            if (slots == null || slots.Count == 0)
            {
                SelectedClockwheelSlot = null;
                return;
            }

            if (SelectedClockwheelSlot == null || !slots.Contains(SelectedClockwheelSlot))
            {
                SelectedClockwheelSlot = slots.FirstOrDefault();
            }
        }

        private void EnsureScheduleSelection()
        {
            var schedule = Settings?.Automation?.RotationSchedule;
            if (schedule == null || schedule.Count == 0)
            {
                SelectedScheduleEntry = null;
                return;
            }

            if (SelectedScheduleEntry == null || !schedule.Contains(SelectedScheduleEntry))
            {
                SelectedScheduleEntry = schedule.FirstOrDefault();
            }
        }

        // TOH slot management
        private void AddTohSlot()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null)
            {
                return;
            }

            var newSlot = new TohSlot
            {
                SlotOrder = slots.Count + 1,
                TrackCount = 1,
                SelectionMode = TohSelectionMode.Random,
                PreventRepeat = true
            };

            slots.Add(newSlot);
            SelectedTohSlot = newSlot;
            NotifySettingsModified();
        }

        private void RemoveSelectedTohSlot()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null || SelectedTohSlot == null)
            {
                return;
            }

            var index = slots.IndexOf(SelectedTohSlot);
            slots.Remove(SelectedTohSlot);
            ReorderTohSlots();

            if (slots.Count > 0)
            {
                SelectedTohSlot = slots[Math.Min(index, slots.Count - 1)];
            }
            else
            {
                SelectedTohSlot = null;
            }

            NotifySettingsModified();
        }

        private bool CanMoveTohSlotUp()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null || SelectedTohSlot == null)
            {
                return false;
            }
            return slots.IndexOf(SelectedTohSlot) > 0;
        }

        private bool CanMoveTohSlotDown()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null || SelectedTohSlot == null)
            {
                return false;
            }
            var index = slots.IndexOf(SelectedTohSlot);
            return index >= 0 && index < slots.Count - 1;
        }

        private void MoveTohSlotUp()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null || SelectedTohSlot == null)
            {
                return;
            }

            var index = slots.IndexOf(SelectedTohSlot);
            if (index > 0)
            {
                slots.Move(index, index - 1);
                ReorderTohSlots();
                NotifySettingsModified();
            }
        }

        private void MoveTohSlotDown()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null || SelectedTohSlot == null)
            {
                return;
            }

            var index = slots.IndexOf(SelectedTohSlot);
            if (index >= 0 && index < slots.Count - 1)
            {
                slots.Move(index, index + 1);
                ReorderTohSlots();
                NotifySettingsModified();
            }
        }

        private void ReorderTohSlots()
        {
            var slots = Settings?.Automation?.TopOfHour?.Slots;
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SlotOrder = i + 1;
            }
        }

        private static AppSettings Clone(AppSettings source)
        {
            var json = JsonSerializer.Serialize(source ?? new AppSettings());
            var clone = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            clone.ApplyDefaults();
            return clone;
        }

        private static string Serialize(AppSettings settings)
        {
            return JsonSerializer.Serialize(settings ?? new AppSettings());
        }

        private static IReadOnlyList<AudioDeviceOption> BuildDeviceOptions(IReadOnlyList<AudioDeviceInfo>? devices, string defaultLabel)
        {
            var options = new ObservableCollection<AudioDeviceOption>
            {
                new AudioDeviceOption(-1, defaultLabel)
            };

            if (devices != null)
            {
                foreach (var device in devices)
                {
                    options.Add(new AudioDeviceOption(device.DeviceNumber, device.ProductName));
                }
            }

            return options;
        }

        private static IReadOnlyList<string> BuildCategoryOptions(IReadOnlyList<LibraryCategory>? categories)
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

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed record AudioDeviceOption(int DeviceNumber, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed record TohCategoryOption(Guid CategoryId, string Name)
    {
        public override string ToString() => Name;
    }
}
