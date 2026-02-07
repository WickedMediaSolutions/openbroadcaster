using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Streaming;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        // Constants - using PascalCase per C# conventions for const fields
        private const int ChatHistoryLimit = 200;
        private const int AutoDjCrossfadeSteps = 20;
        private const int ChatHistoryMaxLines = 200;
        
        private enum DeckAction { Play, Stop, Next }
        private readonly OpenBroadcaster.Core.Services.RadioService _radioService;
        private readonly OpenBroadcaster.Core.Services.TransportService _transportService;
        private readonly OpenBroadcaster.Core.Services.AudioService _audioService;
        private readonly OpenBroadcaster.Core.Messaging.EventBus _eventBus;
        private string _nowPlaying = string.Empty;
        private readonly OpenBroadcaster.Core.Services.QueueService _queueService;
        private readonly OpenBroadcaster.Core.Services.LibraryService _libraryService;
        private readonly OpenBroadcaster.Core.Services.CartWallService _cartWallService;
        private readonly OpenBroadcaster.Core.Overlay.OverlayService? _overlayService;
        private readonly EncoderManager _encoderManager;
        // Runtime services applied from Settings window
        private OpenBroadcaster.Core.Services.TwitchIntegrationService? _twitchService;
        private System.Threading.CancellationTokenSource? _twitchCts;
        private OpenBroadcaster.Core.Services.DirectServer.DirectHttpServer? _directServer;
        private OpenBroadcaster.Core.Services.LoyaltyLedger? _loyaltyLedger;
        private SimpleAutoDjService? _simpleAutoDjService;
        private Guid? _currentlyPlayingTrackId;
        private OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent? _deckAState;
        private OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent? _deckBState;
        private readonly System.Collections.ObjectModel.ObservableCollection<QueueItemViewModel> _queueItems = new();
        private readonly System.Collections.ObjectModel.ObservableCollection<LibraryItemViewModel> _libraryItems = new();
        private readonly System.Collections.ObjectModel.ObservableCollection<OpenBroadcaster.Core.Models.LibraryCategory> _libraryCategories = new();
        private readonly System.Collections.ObjectModel.ObservableCollection<ChatMessageViewModel> _chatMessages = new();
        private readonly System.Collections.ObjectModel.ObservableCollection<string> _encoderLog = new();
        private string _librarySearchText = string.Empty;
        private OpenBroadcaster.Core.Models.LibraryCategory? _selectedLibraryCategory;
        private string _libraryStatusMessage = string.Empty;
        private double _twitchChatFontSize = 12.0;
        private bool _twitchChatEnabled = false;
        private bool _suppressTwitchToggle = false;
        private bool _isTwitchConnecting = false;
        private bool _autoDjEnabled = false;
        private string _autoDjStatusMessage = string.Empty;
        // AutoDJ crossfade state
        private readonly SemaphoreSlim _autoDjCrossfadeSemaphore = new(1, 1);
        private readonly System.TimeSpan _autoDjCrossfadeDuration = System.TimeSpan.FromSeconds(5);
        private readonly System.TimeSpan _autoDjPreloadLeadTime = System.TimeSpan.FromSeconds(10);
        private bool _autoDjCrossfadeInProgress;
        private OpenBroadcaster.Core.Models.DeckIdentifier? _autoDjPreloadedDeck;
        private OpenBroadcaster.Core.Models.DeckIdentifier? _autoDjAnnounceReadyDeck;
        private bool _encodersEnabled = false;
        private string _encoderStatusMessage = "Encoders offline.";
        private bool _micEnabled = false;
        private int _micVolume = 50;
        private int _masterVolume = 80;
        private int _cartWallVolume = 80;
        private int _encoderVolume = 100;
        private readonly System.Func<int, System.Threading.Tasks.Task<string?>>? _filePicker;
        private OpenBroadcaster.Core.Models.AppSettings _appSettings = null!;
        private OpenBroadcaster.Core.Services.AppSettingsStore? _appSettingsStore;

        public MainWindowViewModel(OpenBroadcaster.Core.Services.RadioService radioService, OpenBroadcaster.Core.Services.TransportService transportService, OpenBroadcaster.Core.Services.AudioService audioService, OpenBroadcaster.Core.Messaging.EventBus eventBus, OpenBroadcaster.Core.Services.QueueService queueService, OpenBroadcaster.Core.Services.LibraryService libraryService, OpenBroadcaster.Core.Services.CartWallService cartWallService, OpenBroadcaster.Core.Overlay.OverlayService? overlayService, OpenBroadcaster.Core.Models.AppSettings appSettings, System.Func<int, System.Threading.Tasks.Task<string?>>? filePicker = null)
        {
            _radioService = radioService ?? throw new ArgumentNullException(nameof(radioService));
            _transportService = transportService ?? throw new ArgumentNullException(nameof(transportService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _cartWallService = cartWallService ?? throw new ArgumentNullException(nameof(cartWallService));
            _overlayService = overlayService;

            PlayCommand = new RelayCommand(_ => Play());
            StopCommand = new RelayCommand(_ => Stop());

            DeckA = new DeckViewModel(OpenBroadcaster.Core.Models.DeckIdentifier.A, _transportService, _eventBus, appSettings);
            DeckB = new DeckViewModel(OpenBroadcaster.Core.Models.DeckIdentifier.B, _transportService, _eventBus, appSettings);

            DeckPlayCommand = new RelayCommand(p => ExecuteDeckAction(p, DeckAction.Play));
            DeckStopCommand = new RelayCommand(p => ExecuteDeckAction(p, DeckAction.Stop));
            DeckNextCommand = new RelayCommand(p => ExecuteDeckAction(p, DeckAction.Next));

            AddToQueueCommand = new RelayCommand(p => { var lv = p as LibraryItemViewModel; if (lv != null) AddLibraryItemToQueue(lv); });
            RemoveQueueItemCommand = new RelayCommand(p => { var qv = p as QueueItemViewModel; if (qv != null) RemoveQueueItem(qv); });
            ShuffleQueueCommand = new RelayCommand(_ => _queueService.Shuffle());
            ClearQueueCommand = new RelayCommand(_ => _queueService.Clear());

            TriggerCartCommand = new RelayCommand(p => { if (p is OpenBroadcaster.Core.Models.CartPad pad) _cartWallService.TogglePad(pad.Id); }, _ => true);
            AssignPadCommand = new RelayCommand(p =>
            {
                if (p is OpenBroadcaster.Core.Models.CartPad pad && _filePicker != null)
                {
                    try
                    {
                        var task = _filePicker(pad.Id);
                        _ = task.ContinueWith(t =>
                        {
                            if (!string.IsNullOrWhiteSpace(t.Result))
                            {
                                _cartWallService.AssignPadFile(pad.Id, t.Result);
                            }
                        }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error assigning cart pad file: {ex.Message}");
                    }
                }
            }, _ => true);

            ClearPadCommand = new RelayCommand(p => { if (p is OpenBroadcaster.Core.Models.CartPad pad) _cartWallService.ClearPad(pad.Id); }, _ => true);

            _filePicker = filePicker;
            _appSettings = appSettings ?? new OpenBroadcaster.Core.Models.AppSettings();
            _appSettings.Twitch ??= new OpenBroadcaster.Core.Models.TwitchSettings();
            _appSettingsStore = new OpenBroadcaster.Core.Services.AppSettingsStore();

            // Initialize volume sliders from saved settings
            _masterVolume = _appSettings.Audio.MasterVolumePercent;
            _micVolume = _appSettings.Audio.MicVolumePercent;
            _cartWallVolume = _appSettings.Audio.CartWallVolumePercent;

            _encoderManager = new EncoderManager();
            try { _encoderManager.UpdateConfiguration(_appSettings.Encoder, _appSettings.Audio.EncoderDeviceId); }
            catch (Exception ex) { Debug.WriteLine($"Error updating encoder configuration: {ex.Message}"); }
            try
            {
                _encoderManager.StatusChanged += (_, args) => Dispatcher.UIThread.Post(() =>
                {
                    AppendEncoderLog(args.Status);
                    UpdateEncoderStatusSummary();
                });
                foreach (var status in _encoderManager.SnapshotStatuses())
                {
                    AppendEncoderLog(status);
                }
            }
            catch { }

            try
            {
                if (_appSettings.Encoder?.AutoStart == true && HasEnabledEncoderProfiles())
                {
                    EncodersEnabled = true;
                }
            }
            catch { }

            // Initialize Twitch service and hook events
            EnsureTwitchService();

            // Debug/test helper: when OB_AUTO_TEST_APPLY_SETTINGS=1 is set in the environment,
            // automatically toggle the main output device to the next available device and
            // save the settings to exercise ApplyAudioSettings() without UI interaction.
            try
            {
                var autoTest = System.Environment.GetEnvironmentVariable("OB_AUTO_TEST_APPLY_SETTINGS");
                if (!string.IsNullOrWhiteSpace(autoTest) && autoTest != "0")
                {
                    var playbackDevices = _audioService.GetOutputDevices();
                    if (playbackDevices != null && playbackDevices.Count > 1)
                    {
                        var settingsVm = new OpenBroadcaster.Avalonia.ViewModels.SettingsViewModel(_appSettings, _audioService, new OpenBroadcaster.Core.Services.AppSettingsStore(), _libraryService);
                        // pick the next device after current DeckA setting (wrap)
                        var current = settingsVm.SelectedMainOutputDevice?.DeviceNumber ?? _appSettings.Audio.DeckADeviceId;
                        var idx = playbackDevices.ToList().FindIndex(d => d.DeviceNumber == current);
                        var next = playbackDevices[(idx + 1) % playbackDevices.Count];
                        settingsVm.SelectedMainOutputDevice = next;
                        settingsVm.Save();
                    }
                }
            }
            catch { }

            // subscribe to audio VU updates
            try
            {
                _audioService.VuMetersUpdated += (_, reading) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ProgramVuPercent = reading.Program * 100.0;
                    EncoderVuPercent = reading.Encoder * 100.0;
                    MicVuPercent = reading.Mic * 100.0;
                });

                _audioService.CartLevelChanged += (_, level) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CartVuPercent = level * 100.0;
                });
            }
            catch { }

            // Subscribe to deck state changes to announce now-playing to Twitch
            try
            {
                _eventBus.Subscribe<OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent>(OnDeckStateChanged);
            }
            catch { }

            // Subscribe to deck state changes for AutoDJ crossfade
            try
            {
                _eventBus.Subscribe<OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent>(OnDeckStateChangedForAutoDjCrossfade);
            }
            catch { }

            // Ensure audio service has an initial encoder level matching the UI slider
            try { _audioService.SetEncoderLevel(_encoderVolume / 100.0); }
            catch (Exception ex) { Debug.WriteLine($"Error setting encoder level: {ex.Message}"); }

            // Menu / other UI commands - minimal implementations to finish wiring
            ManageCategoriesCommand = new RelayCommand(_ => { /* TODO: open manage categories dialog */ });
            OpenAboutDialogCommand = new RelayCommand(_ => 
            {
                try
                {
                    var aboutDialog = new OpenBroadcaster.Avalonia.Views.AboutDialog();
                    if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is global::Avalonia.Controls.Window mainWindow)
                    {
                        aboutDialog.ShowDialog(mainWindow);
                    }
                    else
                    {
                        aboutDialog.Show();
                    }
                }
                catch { }
            });
            OpenAppSettingsCommand = new RelayCommand(_ => { /* TODO: show application settings */ });
            AssignCategoriesCommand = new RelayCommand(_ => { /* TODO: open assign-categories UI for SelectedLibraryItem */ });

            // Import commands now wired to real behaviors

            ImportTracksCommand = new AsyncRelayCommand(async _ =>
            {
                var paths = await PickFilesAsync(allowMultiple: true);
                if (paths != null && paths.Any())
                {
                    var categories = _libraryService.GetCategories();
                    System.Collections.Generic.IReadOnlyList<System.Guid>? selectedCategoryIds = null;
                    
                    // If no categories, prompt to create them first
                    if (categories.Count == 0)
                    {
                        LibraryStatusMessage = "No categories found. Please create categories first.";
                        return;
                    }
                    
                    // Show category selector
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window main)
                    {
                        var selector = new OpenBroadcaster.Avalonia.Views.ImportCategorySelectorWindow(categories);
                        var result = await selector.ShowDialog<bool?>(main);
                        if (result != true)
                        {
                            LibraryStatusMessage = "Import cancelled.";
                            return;
                        }
                        selectedCategoryIds = selector.SelectedCategoryIds;
                    }
                    
                    LibraryStatusMessage = $"Importing {paths.Count} file(s). Please wait...";
                    try
                    {
                        await System.Threading.Tasks.Task.Run(() => _libraryService.ImportFiles(paths, selectedCategoryIds));
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            RefreshLibraryItems();
                            LibraryStatusMessage = "Import complete.";
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            LibraryStatusMessage = $"Import failed: {ex.Message}";
                        });
                    }
                }
            });

            ImportFolderCommand = new AsyncRelayCommand(async _ =>
            {
                var path = await PickFolderAsync();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var categories = _libraryService.GetCategories();
                    System.Collections.Generic.IReadOnlyList<System.Guid>? selectedCategoryIds = null;
                    
                    // If no categories, prompt to create them first
                    if (categories.Count == 0)
                    {
                        LibraryStatusMessage = "No categories found. Please create categories first.";
                        return;
                    }
                    
                    // Show category selector
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window main)
                    {
                        var selector = new OpenBroadcaster.Avalonia.Views.ImportCategorySelectorWindow(categories);
                        var result = await selector.ShowDialog<bool?>(main);
                        if (result != true)
                        {
                            LibraryStatusMessage = "Import cancelled.";
                            return;
                        }
                        selectedCategoryIds = selector.SelectedCategoryIds;
                    }
                    
                    LibraryStatusMessage = $"Importing folder '{System.IO.Path.GetFileName(path)}'. Please wait...";
                    try
                    {
                        await System.Threading.Tasks.Task.Run(() => _libraryService.ImportFolder(path, includeSubfolders: true, selectedCategoryIds));
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            RefreshLibraryItems();
                            LibraryStatusMessage = "Import complete.";
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            LibraryStatusMessage = $"Import failed: {ex.Message}";
                        });
                    }
                }
            });

            OpenAppSettingsCommand = new RelayCommand(_ =>
            {
                try
                {
                    if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
                    if (desktop.MainWindow is not Window main) return;
                    var win = new OpenBroadcaster.Avalonia.Views.SettingsWindow();
                    var vm = new OpenBroadcaster.Avalonia.ViewModels.SettingsViewModel(_appSettings, _audioService, new OpenBroadcaster.Core.Services.AppSettingsStore(), _libraryService);
                    win.DataContext = vm;

                    EventHandler<OpenBroadcaster.Core.Models.AppSettings>? settingsHandler = null;
                    settingsHandler = (_, updated) =>
                    {
                        var previous = _appSettings ?? new OpenBroadcaster.Core.Models.AppSettings();
                        _appSettings = updated ?? new OpenBroadcaster.Core.Models.AppSettings();
                        try { _encoderManager.UpdateConfiguration(_appSettings.Encoder, _appSettings.Audio.EncoderDeviceId); } catch { }
                        
                        // Apply Overlay settings
                        try
                        {
                            _overlayService?.UpdateSettings(_appSettings.Overlay);
                        }
                        catch { }

                        // Apply Twitch settings: update and start/stop as indicated
                        try
                        {
                            EnsureTwitchService();
                            try { _twitchService?.UpdateSettings(_appSettings.Twitch); } catch { }

                            if (TwitchChatEnabled)
                            {
                                _ = StartTwitchBridgeAsync();
                            }
                        }
                        catch { }

                        // Apply Direct Server settings: stop existing server and start new one if enabled
                        try
                        {
                            try { _directServer?.Stop(); } catch { }
                            _directServer = null;

                            var ds = _appSettings.DirectServer;
                            if (ds != null && ds.Enabled)
                            {
                                try
                                {
                                    _directServer = new OpenBroadcaster.Core.Services.DirectServer.DirectHttpServer(
                                        ds,
                                        getSnapshot: GetDirectServerSnapshot,
                                        searchLibrary: SearchLibraryForDirectServer,
                                        onSongRequest: HandleDirectServerSongRequest,
                                        getStationName: () => _appSettings?.Overlay?.ApiUsername ?? "OpenBroadcaster"
                                    );
                                    System.Threading.Tasks.Task.Run(() =>
                                    {
                                        try { _directServer?.Start(); } catch { }
                                    });
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // Apply AutoDJ settings: reload persisted AutoDJ config and update runtime service
                        try
                        {
                            try
                            {
                                if (_simpleAutoDjService == null)
                                {
                                    var starter = new OpenBroadcaster.Core.Services.AutoDjSettingsService();
                                    var rots = starter.Rotations ?? new System.Collections.Generic.List<OpenBroadcaster.Core.Automation.SimpleRotation>();
                                    var sched = starter.Schedule ?? new System.Collections.Generic.List<OpenBroadcaster.Core.Automation.SimpleSchedulerEntry>();
                                    var def = starter.DefaultRotationId;
                                    _simpleAutoDjService = new SimpleAutoDjService(_queueService, _libraryService, rots, sched, _appSettings?.Automation?.TargetQueueDepth ?? 5, def);
                                    _simpleAutoDjService.StatusChanged += (_, status) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => AutoDjStatusMessage = status);
                                }

                                var loader = new OpenBroadcaster.Core.Services.AutoDjSettingsService();
                                var rotations = loader.Rotations ?? new System.Collections.Generic.List<OpenBroadcaster.Core.Automation.SimpleRotation>();
                                var schedule = loader.Schedule ?? new System.Collections.Generic.List<OpenBroadcaster.Core.Automation.SimpleSchedulerEntry>();
                                var defaultRotationId = loader.DefaultRotationId;

                                try { _simpleAutoDjService?.UpdateConfiguration(rotations, schedule, defaultRotationId); } catch { }
                                try { _simpleAutoDjService!.Enabled = _appSettings?.Automation?.AutoStartAutoDj ?? false; } catch { }
                                System.Threading.Tasks.Task.Run(() => _simpleAutoDjService?.EnsureQueueDepth());
                            }
                            catch { }
                        }
                        catch { }
                    };

                    vm.SettingsChanged += settingsHandler;
                    // Wire dialog invokers so SettingsViewModel can open Avalonia dialogs with an owner
                    vm.RotationDialogInvoker = async (rot, categoryOptions, existingNames) =>
                    {
                        try
                        {
                            var dlg = new OpenBroadcaster.Avalonia.Views.RotationDialog(rot, categoryOptions, existingNames);
                            return await dlg.ShowDialog<bool?>(main).ConfigureAwait(true);
                        }
                        catch { return null; }
                    };
                    vm.SchedulerDialogInvoker = async (entry, rotations) =>
                    {
                        try
                        {
                            var dlg = new OpenBroadcaster.Avalonia.Views.SchedulerDialog(entry, rotations.ToList());
                            return await dlg.ShowDialog<bool?>(main).ConfigureAwait(true);
                        }
                        catch { return null; }
                    };
                    win.ShowDialog(main);
                    vm.SettingsChanged -= settingsHandler;
                }
                catch { }
            });

            ManageCategoriesCommand = new RelayCommand(_ =>
            {
                try
                {
                    if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
                    if (desktop.MainWindow is not Window main) return;
                    var win = new OpenBroadcaster.Avalonia.Views.CategoryManagerWindow();
                    var vm = new OpenBroadcaster.Avalonia.ViewModels.CategoryManagerViewModel(_libraryService);
                    win.DataContext = vm;
                    win.ShowDialog(main);
                    // refresh library view after potential changes
                    RefreshLibraryItems();
                }
                catch { }
            });

            AssignCategoriesCommand = new RelayCommand(_ =>
            {
                if (SelectedLibraryItem == null)
                {
                    ShowMessage("Assign Categories", "Select a library item first.");
                    return;
                }
                try
                {
                    if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
                    if (desktop.MainWindow is not Window main) return;
                    var win = new OpenBroadcaster.Avalonia.Views.AssignCategoriesWindow();
                    var vm = new OpenBroadcaster.Avalonia.ViewModels.AssignCategoriesViewModel(_libraryService, SelectedLibraryItem.Id);
                    win.DataContext = vm;
                    win.ShowDialog(main);
                    RefreshLibraryItems();
                }
                catch { }
            });

            _radioService.NowPlayingChanged += (s, e) =>
            {
                NowPlaying = _radioService.NowPlaying ?? string.Empty;
            };

            // Initialize observable collections from core services
            RefreshQueueItems();
            RefreshLibraryItems();

            _queueService.QueueChanged += (_, __) => RefreshQueueItems();
            _libraryService.TracksChanged += (_, __) => RefreshLibraryItems();
            _libraryService.CategoriesChanged += (_, __) => RefreshLibraryCategories();

            // Initialize categories and chat
            RefreshLibraryCategories();

            // Start Direct Server at startup if enabled in settings
            try
            {
                var ds = _appSettings?.DirectServer;
                if (ds != null && ds.Enabled)
                {
                    try
                    {
                        _directServer = new OpenBroadcaster.Core.Services.DirectServer.DirectHttpServer(
                            ds,
                            getSnapshot: GetDirectServerSnapshot,
                            searchLibrary: SearchLibraryForDirectServer,
                            onSongRequest: HandleDirectServerSongRequest,
                            getStationName: () => _appSettings.Overlay?.ApiUsername ?? "OpenBroadcaster"
                        );

                        _directServer.ServerStarted += (_, _) => { /* no-op for now */ };
                        _directServer.ServerStopped += (_, _) => { /* no-op for now */ };
                        _directServer.RequestReceived += (_, endpoint) => { /* no-op */ };

                        try { _directServer.Start(); } catch { }
                    }
                    catch { }
                }
            }
            catch { }

            // Initialize SimpleAutoDjService (use persisted AutoDJ settings)
            try
            {
                try
                {
                    var autoDjSettings = new OpenBroadcaster.Core.Services.AutoDjSettingsService();
                    var rotations = autoDjSettings.Rotations ?? new System.Collections.Generic.List<OpenBroadcaster.Core.Automation.SimpleRotation>();
                    var schedule = autoDjSettings.Schedule ?? new System.Collections.Generic.List<OpenBroadcaster.Core.Automation.SimpleSchedulerEntry>();
                    var defaultRotationId = autoDjSettings.DefaultRotationId;

                    _simpleAutoDjService = new SimpleAutoDjService(_queueService, _libraryService, rotations, schedule, _appSettings?.Automation?.TargetQueueDepth ?? 5, defaultRotationId);
                    _simpleAutoDjService.StatusChanged += (_, status) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => AutoDjStatusMessage = status);

                    // Always ensure queue is seeded on startup
                    System.Threading.Tasks.Task.Run(() => _simpleAutoDjService!.EnsureQueueDepth());

                    // Apply auto-start setting from app settings
                    try { _simpleAutoDjService.Enabled = _appSettings?.Automation?.AutoStartAutoDj ?? false; } catch { }
                }
                catch { }
            }
            catch { }
        }

        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand DeckPlayCommand { get; }
        public ICommand DeckStopCommand { get; }
        public ICommand DeckNextCommand { get; }
        public ICommand AddToQueueCommand { get; }
        public ICommand RemoveQueueItemCommand { get; }
        public ICommand ShuffleQueueCommand { get; }
        public ICommand ClearQueueCommand { get; }
        public ICommand TriggerCartCommand { get; }
        public ICommand AssignPadCommand { get; }
        public ICommand ClearPadCommand { get; }
        // Additional UI commands (menu/buttons)
        public ICommand ManageCategoriesCommand { get; }
        public ICommand ImportTracksCommand { get; }
        public ICommand ImportFolderCommand { get; }
        public ICommand OpenAboutDialogCommand { get; }
        public ICommand OpenAppSettingsCommand { get; }
        public ICommand AssignCategoriesCommand { get; }

        public DeckViewModel DeckA { get; }
        public DeckViewModel DeckB { get; }
        public System.Collections.ObjectModel.ReadOnlyObservableCollection<QueueItemViewModel> QueueItems => new(_queueItems);
        public System.Collections.ObjectModel.ReadOnlyObservableCollection<LibraryItemViewModel> LibraryItems => new(_libraryItems);
        public System.Collections.ObjectModel.ReadOnlyObservableCollection<OpenBroadcaster.Core.Models.CartPad> CartPads => _cartWallService?.Pads ?? new System.Collections.ObjectModel.ReadOnlyObservableCollection<OpenBroadcaster.Core.Models.CartPad>(new System.Collections.ObjectModel.ObservableCollection<OpenBroadcaster.Core.Models.CartPad>());
        private QueueItemViewModel? _selectedQueueItem;
        private LibraryItemViewModel? _selectedLibraryItem;
        private global::Avalonia.Media.Imaging.Bitmap? _selectedLibraryItemArt;

        public QueueItemViewModel? SelectedQueueItem
        {
            get => _selectedQueueItem;
            set
            {
                if (_selectedQueueItem != value)
                {
                    _selectedQueueItem = value;
                    OnPropertyChanged();
                }
            }
        }

        public LibraryItemViewModel? SelectedLibraryItem
        {
            get => _selectedLibraryItem;
            set
            {
                if (_selectedLibraryItem != value)
                {
                    _selectedLibraryItem = value;
                    OnPropertyChanged();
                    // Load preview art for selected library item asynchronously
                    LoadSelectedLibraryArtAsync(_selectedLibraryItem);
                }
            }
        }

        public global::Avalonia.Media.Imaging.Bitmap? SelectedLibraryItemArt { get => _selectedLibraryItemArt; private set { if (_selectedLibraryItemArt != value) { _selectedLibraryItemArt = value; OnPropertyChanged(); } } }

        private void LoadSelectedLibraryArtAsync(LibraryItemViewModel? item)
        {
            // fire-and-forget background load
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (item == null) return;
                    var path = item.Underlying.FilePath;
                    var bmp = OpenBroadcaster.Avalonia.Services.AlbumArtService.FetchArtFor(path, item.Title, item.Artist);
                    if (bmp != null)
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedLibraryItemArt = bmp);
                    }
                    else
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedLibraryItemArt = null);
                    }
                }
                catch { }
            });
        }

        public string NowPlaying
        {
            get => _nowPlaying;
            set
            {
                if (_nowPlaying != value)
                {
                    _nowPlaying = value;
                    OnPropertyChanged();
                }
            }
        }

        // --- New properties expected by XAML bindings
        public string LibrarySearchText { get => _librarySearchText; set { if (_librarySearchText != value) { _librarySearchText = value; OnPropertyChanged(); RefreshLibraryItems(); } } }
        // Expose stable collections so bindings can observe the same instance
        public System.Collections.ObjectModel.ObservableCollection<OpenBroadcaster.Core.Models.LibraryCategory> LibraryCategories => _libraryCategories;
        public OpenBroadcaster.Core.Models.LibraryCategory? SelectedLibraryCategory { get => _selectedLibraryCategory; set { if (_selectedLibraryCategory != value) { _selectedLibraryCategory = value; OnPropertyChanged(); RefreshLibraryItems(); } } }
        public string LibraryStatusMessage { get => _libraryStatusMessage; set { if (_libraryStatusMessage != value) { _libraryStatusMessage = value; OnPropertyChanged(); } } }

        public double TwitchChatFontSize { get => _twitchChatFontSize; set { if (Math.Abs(_twitchChatFontSize - value) > 0.01) { _twitchChatFontSize = value; OnPropertyChanged(); } } }
        public System.Collections.ObjectModel.ObservableCollection<ChatMessageViewModel> ChatMessages => _chatMessages;
        public System.Collections.ObjectModel.ObservableCollection<string> EncoderLog => _encoderLog;

        public bool EncodersEnabled
        {
            get => _encodersEnabled;
            set
            {
                if (_encodersEnabled != value)
                {
                    _encodersEnabled = value;
                    OnPropertyChanged();
                    if (_encodersEnabled)
                    {
                        StartEncoders();
                    }
                    else
                    {
                        StopEncoders();
                    }
                }
            }
        }

        public string EncoderStatusMessage
        {
            get => _encoderStatusMessage;
            private set
            {
                if (_encoderStatusMessage != value)
                {
                    _encoderStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool TwitchChatEnabled
        {
            get => _twitchChatEnabled;
            set
            {
                if (_suppressTwitchToggle)
                {
                    if (_twitchChatEnabled != value)
                    {
                        _twitchChatEnabled = value;
                        OnPropertyChanged();
                    }
                    return;
                }

                if (_twitchChatEnabled != value)
                {
                    _twitchChatEnabled = value;
                    OnPropertyChanged();
                    if (value)
                    {
                        _ = StartTwitchBridgeAsync();
                    }
                    else
                    {
                        StopTwitchBridge();
                    }
                }
            }
        }
        public bool AutoDjEnabled
        {
            get => _autoDjEnabled;
            set
            {
                if (_autoDjEnabled == value) return;
                _autoDjEnabled = value;
                OnPropertyChanged();
                try
                {
                    if (_simpleAutoDjService != null)
                    {
                        _simpleAutoDjService.Enabled = value;
                    }
                }
                catch { }
            }
        }
        public string AutoDjStatusMessage { get => _autoDjStatusMessage; set { if (_autoDjStatusMessage != value) { _autoDjStatusMessage = value; OnPropertyChanged(); } } }

        public bool MicEnabled { get => _micEnabled; set { if (_micEnabled != value) { _micEnabled = value; OnPropertyChanged(); try { _audioService.SetMicEnabled(value); } catch { } } } }

        public int MicVolume
        {
            get => _micVolume;
            set
            {
                if (_micVolume != value)
                {
                    _micVolume = value;
                    OnPropertyChanged();
                    try { _audioService.SetMicVolume(_micVolume / 100.0); } catch { }
                    SaveSettings();
                }
            }
        }

        public int MasterVolume
        {
            get => _masterVolume;
            set
            {
                if (_masterVolume != value)
                {
                    _masterVolume = value;
                    OnPropertyChanged();
                    try
                    {
                        _audioService.SetDeckVolume(OpenBroadcaster.Core.Models.DeckIdentifier.A, _masterVolume / 100.0);
                        _audioService.SetDeckVolume(OpenBroadcaster.Core.Models.DeckIdentifier.B, _masterVolume / 100.0);
                    }
                    catch { }
                    SaveSettings();
                }
            }
        }

        public int CartWallVolume
        {
            get => _cartWallVolume;
            set
            {
                if (_cartWallVolume != value)
                {
                    _cartWallVolume = value;
                    OnPropertyChanged();
                    try { _audioService.SetCartVolume(_cartWallVolume / 100.0); } catch { }
                    SaveSettings();
                }
            }
        }

        public int EncoderVolume
        {
            get => _encoderVolume;
            set
            {
                if (_encoderVolume != value)
                {
                    _encoderVolume = value;
                    OnPropertyChanged();
                    try { _audioService.SetEncoderLevel(_encoderVolume / 100.0); } catch { }
                }
            }
        }

        // VU meter percent properties (0..100)
        private double _programVuPercent;
        public double ProgramVuPercent { get => _programVuPercent; private set { if (_programVuPercent != value) { _programVuPercent = value; OnPropertyChanged(); } } }
        private double _encoderVuPercent;
        public double EncoderVuPercent { get => _encoderVuPercent; private set { if (_encoderVuPercent != value) { _encoderVuPercent = value; OnPropertyChanged(); } } }
        private double _micVuPercent;
        public double MicVuPercent { get => _micVuPercent; private set { if (_micVuPercent != value) { _micVuPercent = value; OnPropertyChanged(); } } }
        private double _cartVuPercent;
        public double CartVuPercent { get => _cartVuPercent; private set { if (_cartVuPercent != value) { _cartVuPercent = value; OnPropertyChanged(); } } }

        public void Play()
        {
            _radioService.Play();
        }

        public void Stop()
        {
            _radioService.Stop();
        }

        private bool HasEnabledEncoderProfiles()
        {
            var profiles = _appSettings?.Encoder?.Profiles;
            return profiles != null && profiles.Any(profile => profile.Enabled);
        }

        private void StartEncoders()
        {
            if (!HasEnabledEncoderProfiles())
            {
                AppendEncoderLog(new EncoderStatus(Guid.Empty, "Encoders", EncoderState.Stopped, "No enabled encoder profiles.", null));
                EncoderStatusMessage = "No encoder profiles enabled.";
                _encodersEnabled = false;
                OnPropertyChanged(nameof(EncodersEnabled));
                return;
            }

            try
            {
                _encoderManager.Start();
                UpdateEncoderStatusSummary();
            }
            catch (Exception ex)
            {
                AppendEncoderLog(new EncoderStatus(Guid.Empty, "Encoders", EncoderState.Failed, ex.Message, null));
                EncoderStatusMessage = $"Encoder start failed: {ex.Message}";
                _encodersEnabled = false;
                OnPropertyChanged(nameof(EncodersEnabled));
            }
        }

        private void StopEncoders()
        {
            try
            {
                _encoderManager.Stop();
                UpdateEncoderStatusSummary();
            }
            catch { }
        }

        private void UpdateEncoderStatusSummary()
        {
            var statuses = _encoderManager.SnapshotStatuses()?.ToList() ?? new List<EncoderStatus>();
            if (statuses.Count == 0)
            {
                EncoderStatusMessage = "No encoder profiles configured.";
                return;
            }

            if (statuses.Any(status => status.State == EncoderState.Connecting))
            {
                EncoderStatusMessage = "Encoders connecting...";
                return;
            }

            var streamingCount = statuses.Count(status => status.State == EncoderState.Streaming);
            if (streamingCount > 0)
            {
                EncoderStatusMessage = streamingCount == 1
                    ? "Streaming to 1 target."
                    : $"Streaming to {streamingCount} targets.";
                return;
            }

            var failed = statuses.FirstOrDefault(status => status.State == EncoderState.Failed);
            if (failed != null)
            {
                EncoderStatusMessage = $"Encoder error: {failed.Message}";
                return;
            }

            EncoderStatusMessage = _encodersEnabled ? "Encoders armed." : "Encoders offline.";
        }

        private void AppendEncoderLog(OpenBroadcaster.Core.Streaming.EncoderStatus status)
        {
            if (status == null)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"[{timestamp}] {status.Name}: {status.State} - {status.Message}";
            _encoderLog.Add(line);

            const int maxLines = 200;
            while (_encoderLog.Count > maxLines)
            {
                _encoderLog.RemoveAt(0);
            }

            OnPropertyChanged(nameof(EncoderLog));
        }

        public void AddLibraryItemToQueue(LibraryItemViewModel item)
        {
            if (item == null) return;
            var track = _libraryService.GetTrack(item.Id);
            if (track == null) return;
            var qi = new OpenBroadcaster.Core.Models.QueueItem(track, OpenBroadcaster.Core.Models.QueueSource.Manual, "Library", "Host");
            _queueService.Enqueue(qi);
        }

        /// <summary>
        /// Insert a library item at a specific position in the queue (for drag-and-drop).
        /// </summary>
        public void InsertLibraryItemToQueueAt(LibraryItemViewModel item, int index)
        {
            if (item == null) return;
            var track = _libraryService.GetTrack(item.Id);
            if (track == null) return;
            var qi = new OpenBroadcaster.Core.Models.QueueItem(track, OpenBroadcaster.Core.Models.QueueSource.Manual, "Library", "Host");
            _queueService.InsertAt(index, qi);
        }

        /// <summary>
        /// Load a library item directly to a deck (for drag-and-drop).
        /// </summary>
        public void LoadLibraryItemToDeck(LibraryItemViewModel item, OpenBroadcaster.Core.Models.DeckIdentifier deckId)
        {
            if (item == null) return;
            var track = _libraryService.GetTrack(item.Id);
            if (track == null) return;
            _transportService.LoadTrackToDeck(deckId, track);
        }

        /// <summary>
        /// Reorder queue item from one index to another (for drag-and-drop).
        /// </summary>
        public void ReorderQueueItem(int fromIndex, int toIndex)
        {
            _queueService.Reorder(fromIndex, toIndex);
        }

        /// <summary>
        /// Get the index of a queue item in the current queue.
        /// </summary>
        public int GetQueueItemIndex(QueueItemViewModel item)
        {
            if (item == null) return -1;
            var snapshot = _queueService.Snapshot();
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (ReferenceEquals(snapshot[i], item.Underlying))
                {
                    return i;
                }
            }
            return -1;
        }

        private void RemoveQueueItem(QueueItemViewModel item)
        {
            if (item == null) return;
            var snapshot = _queueService.Snapshot();
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (ReferenceEquals(snapshot[i], item.Underlying))
                {
                    _queueService.RemoveAt(i);
                    return;
                }
            }
        }

        private void RefreshQueueItems()
        {
            _queueItems.Clear();
            foreach (var q in _queueService.Snapshot())
            {
                _queueItems.Add(new QueueItemViewModel(q));
            }
            OnPropertyChanged(nameof(QueueItems));
        }

        private void RefreshLibraryItems()
        {
            _libraryItems.Clear();

            var all = _library_service_get_all();

            // Apply category filter if selected
            if (SelectedLibraryCategory != null)
            {
                var cid = SelectedLibraryCategory.Id;
                all = all.Where(t => t.CategoryIds.Contains(cid)).ToArray();
            }

            // Apply search text filter
            var search = LibrarySearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search;
                all = all.Where(t => (!string.IsNullOrWhiteSpace(t.Title) && t.Title.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                                || (!string.IsNullOrWhiteSpace(t.Artist) && t.Artist.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                                || (!string.IsNullOrWhiteSpace(t.Album) && t.Album.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                                || (!string.IsNullOrWhiteSpace(t.Genre) && t.Genre.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                          .ToArray();
            }

            foreach (var t in all)
            {
                _libraryItems.Add(new LibraryItemViewModel(t));
            }

            OnPropertyChanged(nameof(LibraryItems));
        }

        private void RefreshLibraryCategories()
        {
            _libraryCategories.Clear();
            try
            {
                var cats = _libraryService.GetCategories();
                foreach (var c in cats)
                {
                    _libraryCategories.Add(c);
                }
            }
            catch { }
            OnPropertyChanged(nameof(LibraryCategories));
            try
                {
                    var logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "logs");
                    System.IO.Directory.CreateDirectory(logDir);
                    var dumpPath = System.IO.Path.Combine(logDir, "categories-dump.txt");
                    var dump = new System.Text.StringBuilder();
                    dump.AppendLine($"CategoriesCount: {_libraryCategories.Count}");
                    foreach (var c in _libraryCategories)
                    {
                        dump.AppendLine($"{c.Id} | {c.Name} | {c.Type}");
                    }
                    System.IO.File.AppendAllText(dumpPath, dump.ToString());
                }
            catch { }
        }

        // Helper to avoid large public API changes: call GetTracks()
        private System.Collections.Generic.IReadOnlyCollection<OpenBroadcaster.Core.Models.Track> _library_service_get_all()
            => _libraryService.GetTracks();

        private OpenBroadcaster.Core.Services.DirectServer.DirectServerSnapshot GetDirectServerSnapshot()
        {
            var snapshot = new OpenBroadcaster.Core.Services.DirectServer.DirectServerSnapshot();

            try
            {
                var queue = _queueService.Snapshot();
                var now = queue.FirstOrDefault();
                if (now != null && now.Track != null)
                {
                    snapshot.NowPlaying = new OpenBroadcaster.Core.Services.DirectServer.DirectServerDtos.NowPlayingResponse
                    {
                        TrackId = now.Track.Id.ToString(),
                        Title = now.Track.Title ?? "Unknown",
                        Artist = now.Track.Artist ?? "Unknown",
                        Album = now.Track.Album ?? string.Empty,
                        Duration = (int)(now.Track.Duration.TotalSeconds),
                        Position = 0,
                        IsPlaying = true,
                        RequestedBy = now.Source,
                        Type = "track"
                    };
                }

                snapshot.Queue = queue.Select(q => new OpenBroadcaster.Core.Services.DirectServer.DirectServerDtos.QueueItem
                {
                    Id = q.Track?.Id.ToString(),
                    Title = q.Track?.Title ?? "Unknown",
                    Artist = q.Track?.Artist ?? "Unknown",
                    Album = q.Track?.Album ?? string.Empty,
                    Duration = (int)(q.Track?.Duration.TotalSeconds ?? 0),
                    RequestedBy = q.Source,
                    Type = "track"
                }).ToList();
            }
            catch { }

            return snapshot;
        }

        private System.Collections.Generic.IEnumerable<OpenBroadcaster.Core.Services.DirectServer.DirectServerLibraryItem> SearchLibraryForDirectServer(string query, int page, int perPage)
        {
            try
            {
                var tracks = _libraryService.GetTracks();
                var filtered = tracks
                    .Where(t => string.IsNullOrWhiteSpace(query)
                                || (t.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                                || (t.Artist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                                || (t.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .OrderBy(t => t.Title ?? string.Empty)
                    .Skip(Math.Max(0, (page - 1)) * perPage)
                    .Take(perPage);

                return filtered.Select(t => new OpenBroadcaster.Core.Services.DirectServer.DirectServerLibraryItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    Artist = t.Artist,
                    Album = t.Album,
                    Duration = t.Duration,
                    FilePath = t.FilePath
                });
            }
            catch
            {
                return Array.Empty<OpenBroadcaster.Core.Services.DirectServer.DirectServerLibraryItem>();
            }
        }

        private void HandleDirectServerSongRequest(OpenBroadcaster.Core.Services.DirectServer.SongRequest request)
        {
            try
            {
                var track = _libraryService.GetTracks().FirstOrDefault(t => t.Id == request.TrackId);
                if (track != null)
                {
                    var queueItem = new OpenBroadcaster.Core.Models.QueueItem(
                        track,
                        OpenBroadcaster.Core.Models.QueueSource.WebRequest,
                        "Website Request",
                        request.RequesterName ?? "Website",
                        rotationName: null,
                        categoryName: null,
                        requestMessage: request.Message);
                    _queueService.Enqueue(queueItem);
                }
            }
            catch { }
        }

        private void ExecuteDeckAction(object? parameter, DeckAction action)
        {
            if (parameter == null) return;
            var id = parameter.ToString();
            var deckId = id == "B" ? OpenBroadcaster.Core.Models.DeckIdentifier.B : OpenBroadcaster.Core.Models.DeckIdentifier.A;
            _radioService.ActiveDeck = deckId;
            switch (action)
            {
                case DeckAction.Play:
                    _radioService.Play();
                    break;
                case DeckAction.Stop:
                    _radioService.Stop();
                    break;
                case DeckAction.Next:
                    // Request next from queue then play
                    _transportService.RequestNextFromQueue(deckId);
                    _radioService.Play();
                    break;
            }
        }

        // --- UI helpers: file/folder picking via MainWindow.StorageProvider
        private async System.Threading.Tasks.Task<System.Collections.Generic.List<string>?> PickFilesAsync(bool allowMultiple)
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
                if (desktop.MainWindow is not Window main) return null;
                var sp = main.StorageProvider;
                if (sp == null) return null;

                var options = new global::Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    AllowMultiple = allowMultiple,
                    Title = "Select audio files to import"
                };

                var picked = await sp.OpenFilePickerAsync(options);
                if (picked == null || picked.Count == 0) return null;

                var results = new System.Collections.Generic.List<string>();
                foreach (var f in picked)
                {
                    var localPath = f.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(localPath))
                    {
                        results.Add(localPath);
                    }
                }

                return results.Count > 0 ? results : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PickFilesAsync error: {ex.Message}");
                return null;
            }
        }

        private async System.Threading.Tasks.Task<string?> PickFolderAsync()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
                if (desktop.MainWindow is not Window main) return null;
                var sp = main.StorageProvider;
                if (sp == null) return null;

                var options = new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select folder to import"
                };

                var picked = await sp.OpenFolderPickerAsync(options);
                if (picked == null || picked.Count == 0) return null;

                var folder = picked[0];
                return folder.TryGetLocalPath();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PickFolderAsync error: {ex.Message}");
                return null;
            }
        }

        private void ShowMessage(string title, string message)
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
                if (desktop.MainWindow is not Window main) return;
                var win = new OpenBroadcaster.Avalonia.Views.SimpleMessageWindow(title, message);
                win.ShowDialog(main);
            }
            catch { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void EnsureTwitchService()
        {
            if (_twitchService != null)
            {
                return;
            }

            if (_loyaltyLedger == null)
            {
                _loyaltyLedger = new OpenBroadcaster.Core.Services.LoyaltyLedger();
            }

            _twitchService = new OpenBroadcaster.Core.Services.TwitchIntegrationService(
                _queueService,
                _transportService,
                _loyaltyLedger,
                _libraryService);

            _twitchService.ChatMessageReceived += OnTwitchChatMessage;
            _twitchService.StatusChanged += (_, status) => Dispatcher.UIThread.Post(() => AppendChatMessage("System", status, true));
        }

        private async System.Threading.Tasks.Task StartTwitchBridgeAsync()
        {
            if (_isTwitchConnecting)
            {
                return;
            }

            _isTwitchConnecting = true;
            AppendChatMessage("System", "Connecting to Twitch chat...", true);

            _twitchCts?.Cancel();
            _twitchCts?.Dispose();
            _twitchCts = new System.Threading.CancellationTokenSource();

            try
            {
                EnsureTwitchService();

                var settings = _appSettings.Twitch ?? new OpenBroadcaster.Core.Models.TwitchSettings();
                _appSettings.Twitch = settings;
                _twitchService?.UpdateSettings(settings);

                if (!IsTwitchSettingsValid(settings))
                {
                    AppendChatMessage("System", "Open Twitch settings to configure username, token, and channel.", true);
                    ForceDisableTwitchToggle();
                    return;
                }

                var options = settings.ToChatOptions();
                await _twitchService!.StartAsync(settings, _twitchCts.Token);
                AppendChatMessage("System", $"Connected to #{options.NormalizedChannel}.", true);
            }
            catch (System.OperationCanceledException)
            {
                AppendChatMessage("System", "Twitch chat connection canceled.", true);
            }
            catch (System.Exception ex)
            {
                AppendChatMessage("System", $"Twitch connect failed: {ex.Message}", true);
                ForceDisableTwitchToggle();
            }
            finally
            {
                _isTwitchConnecting = false;
            }
        }

        private void StopTwitchBridge()
        {
            try
            {
                _twitchCts?.Cancel();
                _twitchCts?.Dispose();
                _twitchCts = null;
                _ = _twitchService?.StopAsync();
            }
            catch { }

            AppendChatMessage("System", "Twitch chat offline.", true);
        }

        private void ForceDisableTwitchToggle()
        {
            if (_twitchChatEnabled)
            {
                _suppressTwitchToggle = true;
                _twitchChatEnabled = false;
                OnPropertyChanged(nameof(TwitchChatEnabled));
                _suppressTwitchToggle = false;
            }

            StopTwitchBridge();
        }

        private void OnDeckStateChangedForAutoDjCrossfade(OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent payload)
        {
            if (!_autoDjEnabled)
            {
                return;
            }

            if (payload.Status != OpenBroadcaster.Core.Models.DeckStatus.Playing)
            {
                return;
            }

            // Only crossfade when exactly one deck is currently playing
            var deckA = _transportService.DeckA;
            var deckB = _transportService.DeckB;
            var deckAPlaying = deckA.Status == OpenBroadcaster.Core.Models.DeckStatus.Playing;
            var deckBPlaying = deckB.Status == OpenBroadcaster.Core.Models.DeckStatus.Playing;

            if (deckAPlaying == deckBPlaying)
            {
                return;
            }

            var fromDeck = deckAPlaying ? OpenBroadcaster.Core.Models.DeckIdentifier.A : OpenBroadcaster.Core.Models.DeckIdentifier.B;

            if (payload.DeckId != fromDeck)
            {
                return;
            }

            var toDeck = fromDeck == OpenBroadcaster.Core.Models.DeckIdentifier.A
                ? OpenBroadcaster.Core.Models.DeckIdentifier.B
                : OpenBroadcaster.Core.Models.DeckIdentifier.A;

            // Preload next track at 10 seconds remaining into the non-playing deck.
            if (payload.Remaining <= _autoDjPreloadLeadTime && payload.Remaining > _autoDjCrossfadeDuration)
            {
                var toDeckService = toDeck == OpenBroadcaster.Core.Models.DeckIdentifier.A ? _transportService.DeckA : _transportService.DeckB;
                if (toDeckService.CurrentQueueItem == null && _autoDjPreloadedDeck != toDeck)
                {
                    var next = _transportService.RequestNextFromQueue(toDeck);
                    if (next != null)
                    {
                        _autoDjPreloadedDeck = toDeck;
                    }
                }

                return;
            }

            // Trigger crossfade at 5 seconds remaining.
            if (payload.Remaining <= _autoDjCrossfadeDuration)
            {
                _ = StartAutoDjCrossfadeAsync(fromDeck, toDeck);
            }
        }

        private async System.Threading.Tasks.Task StartAutoDjCrossfadeAsync(OpenBroadcaster.Core.Models.DeckIdentifier fromDeck, OpenBroadcaster.Core.Models.DeckIdentifier toDeck)
        {
            if (!await _autoDjCrossfadeSemaphore.WaitAsync(0).ConfigureAwait(false))
            {
                return;
            }

            _autoDjCrossfadeInProgress = true;
            _autoDjAnnounceReadyDeck = null;

            try
            {
                var fromDeckService = fromDeck == OpenBroadcaster.Core.Models.DeckIdentifier.A ? _transportService.DeckA : _transportService.DeckB;
                var fromTrackId = fromDeckService.CurrentQueueItem?.Track?.Id;

                // The 'toDeck' should already be primed with the next track.
                // If not, we need to load the next track now.
                var toDeckService = toDeck == OpenBroadcaster.Core.Models.DeckIdentifier.A ? _transportService.DeckA : _transportService.DeckB;
                if (toDeckService.CurrentQueueItem == null)
                {
                    // As a fallback, try to load the next track now.
                    var next = _transportService.RequestNextFromQueue(toDeck);
                    if (next == null)
                    {
                        // Nothing in queue, can't continue.
                        return;
                    }
                }

                // Capture current deck volumes
                var fromStartVolume = _audioService.GetDeckVolume(fromDeck);
                if (fromStartVolume <= 0)
                {
                    fromStartVolume = 1.0;
                }

                var toTargetVolume = _audioService.GetDeckVolume(toDeck);
                if (toTargetVolume <= 0)
                {
                    toTargetVolume = fromStartVolume;
                }

                // Start target deck at zero and begin playback
                _audioService.SetDeckVolume(toDeck, 0.0);
                _transportService.Play(toDeck);

                const int steps = AutoDjCrossfadeSteps;
                var stepDelay = System.TimeSpan.FromMilliseconds(_autoDjCrossfadeDuration.TotalMilliseconds / steps);

                for (int i = 1; i <= steps; i++)
                {
                    if (!_autoDjEnabled)
                    {
                        break;
                    }

                    var t = i / (double)steps;
                    _audioService.SetDeckVolume(fromDeck, fromStartVolume * (1.0 - t));
                    _audioService.SetDeckVolume(toDeck, toTargetVolume * t);

                    await System.Threading.Tasks.Task.Delay(stepDelay).ConfigureAwait(false);
                }

                // Stop and unload the source deck without triggering its own auto-advance
                _transportService.IsSkipping = true;
                try
                {
                    _transportService.Stop(fromDeck);
                    // Unload the track that just finished - do not load next track yet
                    // The next track will be loaded by AutoDJ when it determines it's time
                    _transportService.Unload(fromDeck);
                    EnsureCrossfadedDeckCleared(fromDeck, fromTrackId);
                }
                finally
                {
                    _transportService.IsSkipping = false;
                }

                _audioService.SetDeckVolume(toDeck, toTargetVolume);
                _autoDjPreloadedDeck = null;
                _autoDjAnnounceReadyDeck = toDeck;
            }
            catch (System.Exception)
            {
                // Log error silently for now
            }
            finally
            {
                _autoDjCrossfadeInProgress = false;
                _autoDjCrossfadeSemaphore.Release();
            }
        }

        private void EnsureCrossfadedDeckCleared(OpenBroadcaster.Core.Models.DeckIdentifier fromDeck, System.Guid? fromTrackId)
        {
            if (!fromTrackId.HasValue)
            {
                return;
            }

            var deck = fromDeck == OpenBroadcaster.Core.Models.DeckIdentifier.A ? _transportService.DeckA : _transportService.DeckB;
            var currentId = deck.CurrentQueueItem?.Track?.Id;
            if (deck.Status != OpenBroadcaster.Core.Models.DeckStatus.Playing && currentId.HasValue && currentId.Value == fromTrackId.Value)
            {
                _transportService.Unload(fromDeck);
            }
        }

        private void OnTwitchChatMessage(object? sender, OpenBroadcaster.Core.Models.TwitchChatMessage message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                AppendChatMessage(message.UserName, message.Message, message.IsSystem, message.TimestampUtc);
            });
        }

        private void OnDeckStateChanged(OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent e)
        {
            // Track the latest state for each deck
            if (e.DeckId == OpenBroadcaster.Core.Models.DeckIdentifier.A)
            {
                _deckAState = e;
            }
            else if (e.DeckId == OpenBroadcaster.Core.Models.DeckIdentifier.B)
            {
                _deckBState = e;
            }

            // Only announce the deck that's actually playing (not a fading-out deck during crossfade)
            var playingDeck = SelectPlayingDeckForAnnouncement();

            if (_autoDjEnabled && _autoDjCrossfadeInProgress)
            {
                return;
            }
            
            if (playingDeck?.QueueItem?.Track != null && playingDeck.IsPlaying)
            {
                if (_autoDjAnnounceReadyDeck.HasValue && playingDeck.DeckId != _autoDjAnnounceReadyDeck.Value)
                {
                    return;
                }

                var trackId = playingDeck.QueueItem.Track.Id;

                // Skip if this is the same track we've already announced
                if (_currentlyPlayingTrackId == trackId)
                {
                    return;
                }

                // Skip if track hasn't been playing for at least 5 seconds
                if (playingDeck.Elapsed.TotalSeconds < 5)
                {
                    return;
                }

                // Update currently playing track ID and announce
                _currentlyPlayingTrackId = trackId;
                _autoDjAnnounceReadyDeck = null;

                if (_twitchChatEnabled && _twitchService != null && _twitchService.IsConnected)
                {
                    _twitchService.AnnounceNowPlaying(playingDeck.QueueItem);
                }
            }
            else if (playingDeck == null || !playingDeck.IsPlaying)
            {
                // No track is playing, clear the announcement tracker
                _currentlyPlayingTrackId = null;
            }
        }

        private OpenBroadcaster.Core.Messaging.Events.DeckStateChangedEvent? SelectPlayingDeckForAnnouncement()
        {
            var states = new[] { _deckAState, _deckBState };
            return states
                .Where(state => state?.IsPlaying == true && state.QueueItem?.Track != null)
                .OrderBy(state => state!.Elapsed)
                .ThenBy(state => state!.DeckId)
                .FirstOrDefault();
        }

        private void AppendChatMessage(string userName, string message, bool isSystem, System.DateTime? timestampUtc = null)
        {
            var chat = new ChatMessageViewModel
            {
                UserName = userName,
                Message = message,
                Timestamp = FormatTimestamp(timestampUtc ?? System.DateTime.UtcNow)
            };

            _chatMessages.Add(chat);
            TrimChatHistory();
        }

        private void TrimChatHistory()
        {
            // Batch removal to avoid O(n) RemoveAt(0) calls
            int excessCount = _chatMessages.Count - ChatHistoryLimit;
            if (excessCount > 0)
            {
                // Remove in batch from the end (more efficient than individual RemoveAt(0))
                for (int i = 0; i < excessCount; i++)
                {
                    _chatMessages.RemoveAt(0);
                }
            }
        }

        private static string FormatTimestamp(System.DateTime timestampUtc)
        {
            return timestampUtc.ToLocalTime().ToString("HH:mm");
        }

        private void SaveSettings()
        {
            try
            {
                if (_appSettings?.Audio != null && _appSettingsStore != null)
                {
                    _appSettings.Audio.MasterVolumePercent = _masterVolume;
                    _appSettings.Audio.MicVolumePercent = _micVolume;
                    _appSettings.Audio.CartWallVolumePercent = _cartWallVolume;
                    _appSettingsStore.Save(_appSettings);
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private static bool IsTwitchSettingsValid(OpenBroadcaster.Core.Models.TwitchSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(settings.UserName)
                && !string.IsNullOrWhiteSpace(settings.OAuthToken)
                && !string.IsNullOrWhiteSpace(settings.Channel);
        }

        public void Dispose()
        {
            // Dispose deck view models
            DeckA?.Dispose();
            DeckB?.Dispose();

            // Cancel and dispose Twitch CTS
            _twitchCts?.Cancel();
            _twitchCts?.Dispose();

            // Dispose Twitch service
            _twitchService?.Dispose();

            // Stop direct server if running
            try { _directServer?.Stop(); } catch { }
        }
    }

    public class ChatMessageViewModel
    {
        public string Timestamp { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // Minimal RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly System.Action<object?> _execute;
        private readonly System.Func<object?, bool>? _canExecute;

        public RelayCommand(System.Action<object?> execute, System.Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event System.EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
    }
}
