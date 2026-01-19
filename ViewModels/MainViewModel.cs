using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Overlay;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Streaming;
using OpenBroadcaster.Views;

namespace OpenBroadcaster.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private const int CartSlotCount = 21;
        private const int ChatHistoryLimit = 200;

        private readonly QueueService _queueService;
        private readonly TransportService _transportService;
        private readonly AudioService _audioService;
        private readonly CartWallService _cartWallService;
        private readonly LibraryService _libraryService;
        private readonly SimpleAutoDjService _simpleAutoDjService;
        private readonly TohSchedulerService _tohSchedulerService;
        private readonly LoyaltyLedger _loyaltyLedger;
        private readonly AppSettingsStore _appSettingsStore;
        private readonly TwitchIntegrationService _twitchService;
        private readonly TwitchSettingsStore _twitchSettingsStore;
        private readonly EncoderManager _encoderManager;
        private readonly SharedEncoderAudioSource _sharedEncoderSource;
        private readonly OverlayService _overlayService;
        private AppSettings _appSettings;
        private TwitchSettings _twitchSettings;
        private CancellationTokenSource? _twitchCts;
        private bool _twitchChatEnabled;
        private bool _suppressTwitchToggle;
        private bool _isTwitchConnecting;
        private string _twitchStatusMessage = "Twitch chat offline.";
        private string _twitchRequestSummary = "No pending Twitch requests.";
        private readonly IEventBus _eventBus;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDisposable _encoderMetadataSubscription;
        private readonly object _libraryCacheLock = new();
        private List<Track> _libraryTrackCache = new();
        private Dictionary<Guid, LibraryCategory> _libraryCategoryLookup = new();
        private bool _suspendLibraryFilter;
        private bool _clockwheelConfigured;
        private bool _encodersEnabled;
        private bool _suppressEncoderToggle;
        private string _encoderStatusMessage = "Encoders offline.";
        private bool _suppressDeckVolumePersistence;
        private DeckStateChangedEvent? _deckAState;
        private DeckStateChangedEvent? _deckBState;
        private Guid? _lastAnnouncedDeckATrackId;
        private Guid? _lastAnnouncedDeckBTrackId;
        private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
        private string _currentTime = "00:00:00";

        public DeckViewModel DeckA { get; }
        public DeckViewModel DeckB { get; }
        public ObservableCollection<QueueItemViewModel> QueueItems { get; }
        public ObservableCollection<QueueItemViewModel> QueueHistoryItems { get; }
        public ObservableCollection<AutoDjPreviewItemViewModel> AutoDjPreviewItems { get; }
        public ObservableCollection<SongLibraryItemViewModel> LibraryItems { get; }
        public ObservableCollection<LibraryCategoryOption> LibraryCategories { get; }
        public ObservableCollection<TwitchChatMessageViewModel> ChatMessages { get; }
        public ReadOnlyObservableCollection<CartPad> CartPads => _cartWallService.Pads;
        public ObservableCollection<string> CartColorPalette { get; }
        public ObservableCollection<EncoderStatusViewModel> EncoderStatuses { get; }

        /// <summary>
        /// Current time displayed as HH:MM:SS digital clock.
        /// </summary>
        public string CurrentTime
        {
            get => _currentTime;
            private set => SetProperty(ref _currentTime, value);
        }

        public ICommand OpenTwitchSettingsCommand { get; }
        public ICommand OpenAppSettingsCommand { get; }
        public ICommand ManageCategoriesCommand { get; }
        public ICommand RemoveUncategorizedTracksCommand { get; }
        public ICommand CleanupReservedCategoriesCommand { get; }
        public ICommand TriggerCartCommand { get; }
        public ICommand AssignCartFromPickerCommand { get; }
        public ICommand BrowseCartFileCommand { get; }
        public ICommand SaveCartWallCommand { get; }
        public ICommand ImportTracksCommand { get; }
        public ICommand ImportFolderCommand { get; }
        public ICommand AddLibraryItemToQueueCommand { get; }
        public ICommand AssignCategoriesCommand { get; }
        public ICommand RemoveQueueItemCommand { get; }
        public ICommand ClearQueueCommand { get; }
        public ICommand ShuffleQueueCommand { get; }
        public ICommand MoveQueueItemToTopCommand { get; }
        public ICommand MoveQueueItemToBottomCommand { get; }

        private CartPad? _selectedCart;
        public CartPad? SelectedCart
        {
            get => _selectedCart;
            set
            {
                if (SetProperty(ref _selectedCart, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCart));
                    UpdateCartStatus(value);
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasSelectedCart => SelectedCart != null;

        private string _cartEditorStatus = "Select a cart slot to edit.";
        public string CartEditorStatus
        {
            get => _cartEditorStatus;
            private set => SetProperty(ref _cartEditorStatus, value);
        }

        private QueueItemViewModel? _selectedQueueItem;
        public QueueItemViewModel? SelectedQueueItem
        {
            get => _selectedQueueItem;
            set
            {
                if (SetProperty(ref _selectedQueueItem, value))
                {
                    OnPropertyChanged(nameof(HasSelectedQueueItem));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasSelectedQueueItem => SelectedQueueItem != null;

        private SongLibraryItemViewModel? _selectedLibraryItem;
        public SongLibraryItemViewModel? SelectedLibraryItem
        {
            get => _selectedLibraryItem;
            set
            {
                if (SetProperty(ref _selectedLibraryItem, value))
                {
                    OnPropertyChanged(nameof(CanQueueLibrarySelection));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanQueueLibrarySelection => SelectedLibraryItem != null;

        private string _librarySearchText = string.Empty;
        public string LibrarySearchText
        {
            get => _librarySearchText;
            set
            {
                if (SetProperty(ref _librarySearchText, value))
                {
                    ApplyLibraryFilters();
                }
            }
        }

        private LibraryCategoryOption? _selectedLibraryCategory;
        public LibraryCategoryOption? SelectedLibraryCategory
        {
            get => _selectedLibraryCategory;
            set
            {
                if (SetProperty(ref _selectedLibraryCategory, value) && !_suspendLibraryFilter)
                {
                    ApplyLibraryFilters();
                }
            }
        }

        private string _libraryStatusMessage = "Library ready.";
        public string LibraryStatusMessage
        {
            get => _libraryStatusMessage;
            private set => SetProperty(ref _libraryStatusMessage, value);
        }

        private string _nextQueueTitle = "Queue empty.";
        public string NextQueueTitle
        {
            get => _nextQueueTitle;
            private set => SetProperty(ref _nextQueueTitle, value);
        }

        private string _nextQueueArtist = "—";
        public string NextQueueArtist
        {
            get => _nextQueueArtist;
            private set => SetProperty(ref _nextQueueArtist, value);
        }

        private string _nextQueueAttribution = "Add tracks to preview upcoming play.";
        public string NextQueueAttribution
        {
            get => _nextQueueAttribution;
            private set => SetProperty(ref _nextQueueAttribution, value);
        }

        private double _programVu;
        public double ProgramVu
        {
            get => _programVu;
            private set => SetProperty(ref _programVu, value);
        }

        private double _encoderVu;
        public double EncoderVu
        {
            get => _encoderVu;
            private set => SetProperty(ref _encoderVu, value);
        }

        private double _micVu;
        public double MicVu
        {
            get => _micVu;
            private set => SetProperty(ref _micVu, value);
        }

        public bool EncodersEnabled
        {
            get => _encodersEnabled;
            set
            {
                if (_suppressEncoderToggle)
                {
                    SetProperty(ref _encodersEnabled, value, nameof(EncodersEnabled));
                    return;
                }

                if (SetProperty(ref _encodersEnabled, value))
                {
                    if (value)
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
            private set => SetProperty(ref _encoderStatusMessage, value);
        }

        private bool _autoDjEnabled;
        public bool AutoDjEnabled
        {
            get => _autoDjEnabled;
            set
            {
                if (SetProperty(ref _autoDjEnabled, value))
                {
                    _simpleAutoDjService.Enabled = value;
                    _tohSchedulerService.IsAutoDjRunning = value;
                    SyncAutoDjPreview();
                    
                    // Auto-play on Deck A when AutoDJ is enabled
                    if (value && DeckA != null)
                    {
                        var deckA = _transportService.DeckA;
                        if (deckA.CurrentQueueItem == null || deckA.Status == DeckStatus.Empty)
                        {
                            // Load and play from queue
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                System.Threading.Thread.Sleep(500); // Small delay to ensure queue is populated
                                RunOnUiThread(() =>
                                {
                                    if (_autoDjEnabled) // Check again in case it was toggled off
                                    {
                                        _transportService.Play(DeckIdentifier.A);
                                    }
                                });
                            });
                        }
                        else if (deckA.Status != DeckStatus.Playing)
                        {
                            _transportService.Play(DeckIdentifier.A);
                        }
                    }
                }
            }
        }

        private bool _micEnabled;
        public bool MicEnabled
        {
            get => _micEnabled;
            set
            {
                if (SetProperty(ref _micEnabled, value))
                {
                    // This assumes a method exists on the audio service to control the mic.
                    _audioService.SetMicEnabled(value);
                }
            }
        }

        private int _micVolume = 100;
        public int MicVolume
        {
            get => _micVolume;
            set
            {
                if (SetProperty(ref _micVolume, value))
                {
                    var normalizedVolume = Math.Clamp(value, 0, 100) / 100.0;
                    _audioService.SetMicVolume(normalizedVolume);
                    
                    // Save to settings
                    _appSettings.Audio.MicVolumePercent = Math.Clamp(value, 0, 100);
                    _appSettingsStore.Save(_appSettings);
                }
            }
        }

        private int _cartWallVolume = 100;
        public int CartWallVolume
        {
            get => _cartWallVolume;
            set
            {
                if (SetProperty(ref _cartWallVolume, value))
                {
                    var normalizedVolume = Math.Clamp(value, 0, 100) / 100.0;
                    _audioService.SetCartVolume(normalizedVolume);
                    
                    // Save to settings
                    _appSettings.Audio.CartWallVolumePercent = Math.Clamp(value, 0, 100);
                    _appSettingsStore.Save(_appSettings);
                }
            }
        }

        private string _autoDjStatusMessage = "AutoDJ offline.";
        public string AutoDjStatusMessage
        {
            get => _autoDjStatusMessage;
            private set => SetProperty(ref _autoDjStatusMessage, value);
        }

        public bool TwitchChatEnabled
        {
            get => _twitchChatEnabled;
            set
            {
                if (_suppressTwitchToggle)
                {
                    SetProperty(ref _twitchChatEnabled, value);
                    return;
                }

                if (SetProperty(ref _twitchChatEnabled, value))
                {
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

        public string TwitchStatusMessage
        {
            get => _twitchStatusMessage;
            private set => SetProperty(ref _twitchStatusMessage, value);
        }

        public string TwitchRequestSummary
        {
            get => _twitchRequestSummary;
            private set => SetProperty(ref _twitchRequestSummary, value);
        }

        public MainViewModel()
        {
            _logger = Core.Diagnostics.AppLogger.CreateLogger<MainViewModel>();
            _appSettingsStore = new AppSettingsStore();
            _appSettings = _appSettingsStore.Load();
            _eventBus = new EventBus();
            _encoderMetadataSubscription = _eventBus.Subscribe<DeckStateChangedEvent>(OnDeckStateChangedForEncoderMetadata);
            _queueService = new QueueService();
            _queueService.QueueChanged += OnQueueServiceChanged;
            _queueService.HistoryChanged += OnQueueHistoryChanged;
            _audioService = new AudioService();
            _audioService.VuMetersUpdated += OnVuMetersUpdated;
            _transportService = new TransportService(_eventBus, _queueService, _audioService);
            _cartWallService = new CartWallService(_audioService, null, CartSlotCount, SynchronizationContext.Current);
            _libraryService = new LibraryService();

            // Load AutoDJ rotations/schedule/default from persisted service
            var autoDjSettings = new AutoDjSettingsService();
            var rotations = autoDjSettings.Rotations ?? new List<SimpleRotation>();
            var schedule = autoDjSettings.Schedule ?? new List<SimpleSchedulerEntry>();
            var defaultRotationId = autoDjSettings.DefaultRotationId;
            if (defaultRotationId == Guid.Empty && rotations.Count > 0)
            {
                defaultRotationId = rotations.First().Id;
            }

            _simpleAutoDjService = new SimpleAutoDjService(
                _queueService,
                _libraryService,
                rotations,
                schedule,
                Math.Max(5, _appSettings.Automation?.TargetQueueDepth ?? 5),
                defaultRotationId);
            _simpleAutoDjService.StatusChanged += (_, status) => RunOnUiThread(() => AutoDjStatusMessage = status);
            // Note: SimpleAutoDjService already subscribes to QueueChanged internally - do not duplicate!
            
            // Initialize Top-of-Hour scheduler
            _tohSchedulerService = new TohSchedulerService(_queueService, _libraryService);
            _tohSchedulerService.StatusChanged += (_, status) => RunOnUiThread(() => System.Diagnostics.Debug.WriteLine($"[TOH] {status}"));
            _tohSchedulerService.TohFired += OnTohFired;
            
            // CRITICAL: Always populate queue on startup, regardless of AutoDJ enabled state
            _logger.LogInformation("Populating initial queue from active rotation...");
            System.Threading.Tasks.Task.Run(() => _simpleAutoDjService.EnsureQueueDepth());
            _loyaltyLedger = new LoyaltyLedger();
            _twitchSettingsStore = new TwitchSettingsStore();
            _sharedEncoderSource = new SharedEncoderAudioSource(_audioService.RoutingGraph);
            _audioService.AttachEncoderTap(_sharedEncoderSource);
            _encoderManager = new EncoderManager(new RouterEncoderAudioSourceFactory(_sharedEncoderSource));
            _overlayService = new OverlayService(_queueService, _eventBus);
            _encoderManager.StatusChanged += OnEncoderStatusChanged;
            _appSettings = _appSettingsStore.Load();
            _audioService.ApplyAudioSettings(_appSettings.Audio);
            ApplyQueueSettings(_appSettings.Queue);
            _twitchSettings = _twitchSettingsStore.Load();
            _twitchService = new TwitchIntegrationService(_queueService, _transportService, _loyaltyLedger, _libraryService);
            _twitchService.UpdateSettings(_twitchSettings);
            _twitchService.ChatMessageReceived += OnTwitchChatMessage;
            _twitchService.StatusChanged += (_, status) => RunOnUiThread(() => TwitchStatusMessage = status);
            _simpleAutoDjService.StatusChanged += (_, status) => RunOnUiThread(() => AutoDjStatusMessage = status);
            _libraryService.TracksChanged += OnLibraryTracksChanged;
            _libraryService.CategoriesChanged += OnLibraryCategoriesChanged;

            QueueItems = new ObservableCollection<QueueItemViewModel>();
            QueueHistoryItems = new ObservableCollection<QueueItemViewModel>();
            AutoDjPreviewItems = new ObservableCollection<AutoDjPreviewItemViewModel>();
            LibraryItems = new ObservableCollection<SongLibraryItemViewModel>();
            LibraryCategories = new ObservableCollection<LibraryCategoryOption>();
            ChatMessages = new ObservableCollection<TwitchChatMessageViewModel>();
            CartColorPalette = new ObservableCollection<string>(BuildCartColorPalette());
            EncoderStatuses = new ObservableCollection<EncoderStatusViewModel>();

            OpenTwitchSettingsCommand = new RelayCommand(_ => OpenTwitchSettingsDialog());
            OpenAppSettingsCommand = new RelayCommand(_ => OpenAppSettingsDialog());
            ManageCategoriesCommand = new RelayCommand(_ => OpenManageCategoriesDialog());
            RemoveUncategorizedTracksCommand = new RelayCommand(_ => RemoveUncategorizedTracks());
            CleanupReservedCategoriesCommand = new RelayCommand(_ => CleanupReservedCategories());
            TriggerCartCommand = new RelayCommand(p => ToggleCartPad(p as CartPad), p => p is CartPad);
            AssignCartFromPickerCommand = new RelayCommand(p => AssignCartPadFromPicker(p as CartPad), p => p is CartPad);
            BrowseCartFileCommand = new RelayCommand(_ => BrowseCartFile(), _ => HasSelectedCart);
            SaveCartWallCommand = new RelayCommand(_ => PersistCartPads(), _ => CartPads.Count > 0);
            ImportTracksCommand = new RelayCommand(async _ => await ImportLibraryFilesAsync());
            ImportFolderCommand = new RelayCommand(async _ => await ImportLibraryFolderAsync());
            AddLibraryItemToQueueCommand = new RelayCommand(_ => QueueSelectedLibraryTrack(), _ => CanQueueLibrarySelection);
            AssignCategoriesCommand = new RelayCommand(_ => OpenAssignCategoriesDialog(), _ => SelectedLibraryItem != null);
            RemoveQueueItemCommand = new RelayCommand(_ => RemoveSelectedQueueItem(), _ => HasSelectedQueueItem);
            ClearQueueCommand = new RelayCommand(_ => ClearQueue(), _ => QueueItems.Count > 0);
            ShuffleQueueCommand = new RelayCommand(_ => ShuffleQueue(), _ => QueueItems.Count > 1);
            MoveQueueItemToTopCommand = new RelayCommand(_ => MoveSelectedQueueItemToTop(), _ => CanMoveQueueItemUp());
            MoveQueueItemToBottomCommand = new RelayCommand(_ => MoveSelectedQueueItemToBottom(), _ => CanMoveQueueItemDown());

            InitializeLibrary();
            // SeedQueue(); // REMOVED - Let AutoDJ populate queue from active rotation instead
            SeedChat();
            InitializeCartWall();
            UpdateEncoderConfiguration(_appSettings);
            _overlayService.UpdateSettings(_appSettings.Overlay);
            ApplyRequestSettings(_appSettings.Requests);
            ApplyAutomationSettings(_appSettings.Automation);
            
            // Start TOH scheduler (runs in background monitoring time)
            _tohSchedulerService.Start();

            var deckAVolume = Math.Clamp(_appSettings.Audio?.DeckAVolumePercent ?? 100, 0, 100);
            var deckBVolume = Math.Clamp(_appSettings.Audio?.DeckBVolumePercent ?? 100, 0, 100);
            _cartWallVolume = Math.Clamp(_appSettings.Audio?.CartWallVolumePercent ?? 100, 0, 100);
            _micVolume = Math.Clamp(_appSettings.Audio?.MicVolumePercent ?? 100, 0, 100);
            _audioService.SetMicVolume(_micVolume / 100.0);

            _suppressDeckVolumePersistence = true;
            DeckA = new DeckViewModel(
                "Deck A",
                DeckIdentifier.A,
                _transportService,
                _eventBus,
                percent => ApplyDeckVolume(DeckIdentifier.A, percent),
                deckAVolume);
            DeckB = new DeckViewModel(
                "Deck B",
                DeckIdentifier.B,
                _transportService,
                _eventBus,
                percent => ApplyDeckVolume(DeckIdentifier.B, percent),
                deckBVolume);
            _suppressDeckVolumePersistence = false;

            SyncQueueFromService();
            SyncQueueHistoryFromService();
            PrimeDecks();
            SyncAutoDjPreview();

            // Initialize digital clock timer
            _clockTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }

        private void InitializeLibrary()
        {
            // Don't seed hardcoded tracks or categories - let users create their own
            RefreshCategorySnapshot();
            RefreshLibrarySnapshot();
            SyncRotationSources();
        }

        private void EnsureDefaultLibraryTracks()
        {
            // REMOVED - No longer auto-creating sample tracks
            var tracks = _libraryService.GetTracks();
            lock (_libraryCacheLock)
            {
                _libraryTrackCache = tracks.ToList();
            }
        }

        private void EnsureDefaultCategories()
        {
            // REMOVED - No longer auto-creating hardcoded categories
            var categories = _libraryService.GetCategories();
            lock (_libraryCacheLock)
            {
                _libraryCategoryLookup = categories.ToDictionary(static category => category.Id);
            }
        }

        private static IEnumerable<Track> BuildDefaultLibraryTracks()
        {
            // REMOVED - No longer providing sample tracks
            yield break;
        }

        private static IEnumerable<(string name, string type)> BuildDefaultCategories()
        {
            // REMOVED - No longer providing hardcoded categories
            return new List<(string, string)>();
        }

        private void RefreshLibrarySnapshot()
        {
            var tracks = _libraryService.GetTracks();
            lock (_libraryCacheLock)
            {
                _libraryTrackCache = tracks.ToList();
            }

            ApplyLibraryFilters();
        }

        private void RefreshCategorySnapshot()
        {
            var categories = _libraryService.GetCategories();
            lock (_libraryCacheLock)
            {
                _libraryCategoryLookup = categories.ToDictionary(static category => category.Id);
            }

            UpdateCategoryOptions();
        }

        private void UpdateCategoryOptions()
        {
            LibraryCategoryOption[] options;
            lock (_libraryCacheLock)
            {
                var ordered = _libraryCategoryLookup.Values
                    .OrderBy(static category => category.Type, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static category => category.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(LibraryCategoryOption.FromCategory)
                    .ToList();

                options = new[] { LibraryCategoryOption.CreateAll() }
                    .Concat(ordered)
                    .ToArray();
            }

            RunOnUiThread(() =>
            {
                var currentSelection = SelectedLibraryCategory?.CategoryId ?? Guid.Empty;
                _suspendLibraryFilter = true;
                LibraryCategories.Clear();
                foreach (var option in options)
                {
                    LibraryCategories.Add(option);
                }

                var nextSelection = LibraryCategories.FirstOrDefault(option => option.CategoryId == currentSelection)
                    ?? LibraryCategories.FirstOrDefault();
                SelectedLibraryCategory = nextSelection;
                _suspendLibraryFilter = false;
                ApplyLibraryFilters();
            });
        }

        private void ApplyLibraryFilters()
        {
            List<SongLibraryItemViewModel> filtered;
            int totalCount;

            lock (_libraryCacheLock)
            {
                var search = LibrarySearchText?.Trim() ?? string.Empty;
                var selectedCategory = SelectedLibraryCategory?.CategoryId ?? Guid.Empty;
                totalCount = _libraryTrackCache.Count;
                var categoryLookup = _libraryCategoryLookup;

                filtered = _libraryTrackCache
                    .Where(track => selectedCategory == Guid.Empty || track.CategoryIds.Contains(selectedCategory))
                    .Where(track => TrackMatchesSearch(track, search))
                    .Select(track => ToSongLibraryItem(track, categoryLookup))
                    .ToList();
            }

            RunOnUiThread(() =>
            {
                SyncCollection(LibraryItems, filtered);
                LibraryStatusMessage = filtered.Count == totalCount
                    ? $"Showing {filtered.Count} track(s)."
                    : $"Showing {filtered.Count} of {totalCount} track(s).";
            });
        }

        private static bool TrackMatchesSearch(Track track, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            var comparison = StringComparison.OrdinalIgnoreCase;
            return track.Title.Contains(search, comparison)
                || track.Artist.Contains(search, comparison)
                || track.Album.Contains(search, comparison)
                || track.Genre.Contains(search, comparison);
        }

        private static SongLibraryItemViewModel ToSongLibraryItem(Track track, IReadOnlyDictionary<Guid, LibraryCategory> categories)
        {
            var categoryNames = track.CategoryIds
                .Select(id => categories.TryGetValue(id, out var category) ? category.Name : null)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            return new SongLibraryItemViewModel
            {
                TrackId = track.Id,
                Title = track.Title,
                Artist = track.Artist,
                Album = track.Album,
                Genre = track.Genre,
                Duration = track.Duration.ToString(@"mm\:ss"),
                Categories = categoryNames.Length == 0 ? "Uncategorized" : string.Join(", ", categoryNames),
                Year = track.Year > 0 ? track.Year.ToString() : string.Empty,
                FilePath = track.FilePath,
                IsEnabled = track.IsEnabled,
                CategoryIds = track.CategoryIds.ToArray()
            };
        }

        private void OnLibraryTracksChanged(object? sender, EventArgs e)
        {
            RefreshLibrarySnapshot();
            SyncRotationSources();
        }

        private void OnLibraryCategoriesChanged(object? sender, EventArgs e)
        {
            RefreshCategorySnapshot();
            SyncRotationSources();
        }

        private void OnTohFired(object? sender, TohFiredEventArgs e)
        {
            RunOnUiThread(() =>
            {
                SyncQueueFromService();
                SyncAutoDjPreview();
                System.Diagnostics.Debug.WriteLine($"[TOH] Fired: {e.TrackCount} tracks inserted");
            });
        }

        private void QueueSelectedLibraryTrack()
        {
            var selected = SelectedLibraryItem;
            if (selected == null)
            {
                LibraryStatusMessage = "Select a track before queuing.";
                return;
            }

            QueueLibraryTracks(new[] { selected.TrackId });
        }

        public void QueueLibraryTracks(IEnumerable<Guid> trackIds)
        {
            if (trackIds == null)
            {
                LibraryStatusMessage = "No tracks were provided for queuing.";
                return;
            }

            var queued = 0;
            foreach (var trackId in trackIds)
            {
                if (trackId == Guid.Empty)
                {
                    continue;
                }

                var track = _libraryService.GetTrack(trackId);
                if (track == null)
                {
                    continue;
                }

                var sourceLabel = ResolveManualSourceLabel("Library");
                var queueItem = new QueueItem(track, QueueSource.Manual, sourceLabel, string.Empty);
                _queueService.Enqueue(queueItem);
                queued++;
            }

            if (queued > 0)
            {
                SyncQueueFromService();
                LibraryStatusMessage = queued == 1
                    ? "Queued 1 track from library."
                    : $"Queued {queued} tracks from library.";
            }
            else
            {
                LibraryStatusMessage = "Unable to queue the requested track(s).";
            }
        }

        private string ResolveManualSourceLabel(string fallback)
        {
            var label = _appSettings?.Queue?.DefaultSourceLabel;
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.IsNullOrWhiteSpace(fallback) ? "Library" : fallback;
            }

            return label.Trim();
        }

        private void SyncRotationSources()
        {
            var definitions = BuildRotationDefinitions();
            // Legacy: _rotationEngine.LoadCategories(definitions); (removed)

            EnsureClockwheelConfigured();
            SyncAutoDjPreview();
        }

        private IReadOnlyList<RotationCategoryDefinition> BuildRotationDefinitions()
        {
            var results = new List<RotationCategoryDefinition>();
            List<Track> tracksSnapshot;
            Dictionary<Guid, LibraryCategory> categorySnapshot;
            lock (_libraryCacheLock)
            {
                tracksSnapshot = _libraryTrackCache.ToList();
                categorySnapshot = new Dictionary<Guid, LibraryCategory>(_libraryCategoryLookup);
            }

            foreach (var category in categorySnapshot.Values)
            {
                var members = tracksSnapshot
                    .Where(track => track.IsEnabled && track.CategoryIds.Contains(category.Id))
                    .ToList();

                if (members.Count > 0)
                {
                    results.Add(new RotationCategoryDefinition(category.Name, members));
                }
            }

            var uncategorized = tracksSnapshot
                .Where(track => track.IsEnabled && track.CategoryIds.Count == 0)
                .ToList();

            if (uncategorized.Count > 0)
            {
                results.Add(new RotationCategoryDefinition("Uncategorized", uncategorized));
            }

            if (results.Count == 0 && tracksSnapshot.Count > 0)
            {
                results.Add(new RotationCategoryDefinition("Library", tracksSnapshot));
            }

            return results;
        }

        private void EnsureClockwheelConfigured()
        {
            if (_clockwheelConfigured)
            {
                return;
            }

            var slots = BuildDefaultClockwheelSlots();
            if (slots.Count == 0)
            {
                return;
            }

            // Legacy: _clockwheelScheduler.LoadSlots(slots); (removed)
            _clockwheelConfigured = true;
        }

        private IReadOnlyList<ClockwheelSlot> BuildDefaultClockwheelSlots()
        {
            List<string> available;
            lock (_libraryCacheLock)
            {
                available = _libraryCategoryLookup.Values
                    .Select(static category => category.Name)
                    .ToList();
            }

            if (available.Count == 0)
            {
                return new[] { new ClockwheelSlot("Library") };
            }

            string Resolve(string desired)
            {
                var match = available.FirstOrDefault(name => name.Equals(desired, StringComparison.OrdinalIgnoreCase));
                return match ?? available[0];
            }

            var pattern = new[] { "Currents", "Gold", "Currents", "Imaging", "Currents", "News" };
            var slots = new List<ClockwheelSlot>();
            foreach (var label in pattern)
            {
                var categoryName = Resolve(label);
                slots.Add(new ClockwheelSlot(categoryName, null, label));
            }

            return slots;
        }

        private void SyncAutoDjPreview()
        {
            var previewTracks = _simpleAutoDjService.GetUpcomingPreview(5);
            var preview = previewTracks.Select(track => new AutoDjPreviewItemViewModel(
                string.Join(", ", track.CategoryIds), // Could resolve category names if needed
                track.Title,
                track.Artist)).ToList();
            RunOnUiThread(() => SyncCollection(AutoDjPreviewItems, preview));
        }

        private void OnVuMetersUpdated(object? sender, VuMeterReading reading)
        {
            RunOnUiThread(() => UpdateVuMeters(reading));
        }

        private void UpdateVuMeters(VuMeterReading reading)
        {
            ProgramVu = reading.Program;
            EncoderVu = reading.Encoder;
            MicVu = reading.Mic;
        }

        public bool LoadDeckFromLibrary(Guid trackId, DeckIdentifier deckId, bool autoPlay = false)
        {
            if (trackId == Guid.Empty)
            {
                LibraryStatusMessage = "Cannot load deck: missing track selection.";
                return false;
            }

            var track = _libraryService.GetTrack(trackId);
            if (track == null)
            {
                LibraryStatusMessage = "That track is no longer available.";
                return false;
            }

            var queueItem = new QueueItem(track, QueueSource.Manual, ResolveManualSourceLabel("Library"), string.Empty);
            _queueService.EnqueueFront(queueItem);
            var loaded = _transportService.RequestNextFromQueue(deckId);
            if (loaded == null)
            {
                LibraryStatusMessage = "Unable to load deck from queue.";
                return false;
            }

            if (autoPlay)
            {
                _transportService.Play(deckId);
            }

            LibraryStatusMessage = $"Loaded '{loaded.Track.Title}' into Deck {deckId}.";
            return true;
        }

        private void OpenAssignCategoriesDialog()
        {
            var selected = SelectedLibraryItem;
            if (selected == null)
            {
                LibraryStatusMessage = "Select a track to edit categories.";
                return;
            }

            var track = _libraryService.GetTrack(selected.TrackId);
            if (track == null)
            {
                LibraryStatusMessage = "The selected track is no longer available.";
                return;
            }

            var categories = _libraryService.GetCategories();
            var dialogVm = new AssignCategoriesViewModel(
                track.Id,
                track.Title,
                categories,
                track.CategoryIds,
                AddLibraryCategory);

            var window = new AssignCategoriesWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = dialogVm
            };

            var result = window.ShowDialog();
            if (result == true)
            {
                try
                {
                    _ = _libraryService.AssignCategories(track.Id, dialogVm.SelectedCategoryIds);
                    LibraryStatusMessage = $"Updated categories for '{track.Title}'.";
                }
                catch (Exception ex)
                {
                    LibraryStatusMessage = $"Category update failed: {ex.Message}";
                }
            }
        }

        private LibraryCategory AddLibraryCategory(string name, string type)
        {
            return _libraryService.AddCategory(name, type);
        }

        private async Task ImportLibraryFilesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.aac;*.wma;*.ogg;*.m4a;*.aif;*.aiff;*.opus|All Files|*.*",
                Multiselect = true,
                Title = "Import audio files"
            };

            var result = dialog.ShowDialog();
            if (result == true && dialog.FileNames.Length > 0)
            {
                var categories = _libraryService.GetCategories();
                
                // If no categories exist, prompt to create them first
                if (categories.Count == 0)
                {
                    var createCats = System.Windows.MessageBox.Show(
                        "No categories found. Create categories first to organize your tracks.\n\nOpen Manage Categories now?",
                        "No Categories",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);
                    
                    if (createCats == System.Windows.MessageBoxResult.Yes)
                    {
                        OpenManageCategoriesDialog();
                        categories = _libraryService.GetCategories();
                        if (categories.Count == 0)
                        {
                            LibraryStatusMessage = "Import cancelled - no categories available.";
                            return;
                        }
                    }
                    else
                    {
                        // Allow import without categories
                        await RunLibraryImportAsync(() => _libraryService.ImportFiles(dialog.FileNames, null),
                            $"Importing {dialog.FileNames.Length} file(s)...");
                        return;
                    }
                }
                
                var categorySelector = new ImportCategorySelectorWindow(categories)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (categorySelector.ShowDialog() == true)
                {
                    var selectedCategories = categorySelector.SelectedCategoryIds;
                    await RunLibraryImportAsync(() => _libraryService.ImportFiles(dialog.FileNames, selectedCategories),
                        $"Importing {dialog.FileNames.Length} file(s)...");
                }
            }
        }

        private async Task ImportLibraryFolderAsync()
        {
            var folderPath = SelectFolderPath();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            var categories = _libraryService.GetCategories();
            
            // If no categories exist, prompt to create them first
            if (categories.Count == 0)
            {
                var createCats = System.Windows.MessageBox.Show(
                    "No categories found. Create categories first to organize your tracks.\n\nOpen Manage Categories now?",
                    "No Categories",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);
                
                if (createCats == System.Windows.MessageBoxResult.Yes)
                {
                    OpenManageCategoriesDialog();
                    categories = _libraryService.GetCategories();
                    if (categories.Count == 0)
                    {
                        LibraryStatusMessage = "Import cancelled - no categories available.";
                        return;
                    }
                }
                else
                {
                    // Allow import without categories
                    await RunLibraryImportAsync(() => _libraryService.ImportFolder(folderPath, includeSubfolders: true, null),
                        $"Scanning '{folderPath}' for new audio files...");
                    return;
                }
            }
            
            var categorySelector = new ImportCategorySelectorWindow(categories)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (categorySelector.ShowDialog() == true)
            {
                var selectedCategories = categorySelector.SelectedCategoryIds;
                await RunLibraryImportAsync(() => _libraryService.ImportFolder(folderPath, includeSubfolders: true, selectedCategories),
                    $"Scanning '{folderPath}' for new audio files...");
            }
        }

        private async Task RunLibraryImportAsync(Func<IReadOnlyCollection<Track>> importFunc, string pendingMessage)
        {
            LibraryStatusMessage = pendingMessage + " Please wait...";

            try
            {
                var imported = await Task.Run(importFunc);
                
                LibraryStatusMessage = imported.Count == 0
                    ? "No changes made. Files may already be imported with the selected categories."
                    : $"Successfully processed {imported.Count} track(s). New tracks were added and existing tracks had categories updated.";
            }
            catch (Exception ex)
            {
                LibraryStatusMessage = $"Import failed: {ex.Message}";
            }
        }

        private static string? SelectFolderPath()
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select a music folder to scan",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            var result = dialog.ShowDialog();
            return result == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        private static void SyncCollection<T>(ObservableCollection<T> target, IList<T> source)
        {
            target.Clear();
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private static SongLibraryItemViewModel ToSongLibraryItem(Track track)
        {
            return new SongLibraryItemViewModel
            {
                Title = track.Title,
                Artist = track.Artist,
                Album = track.Album,
                Genre = track.Genre,
                Duration = track.Duration.ToString(@"mm\:ss")
            };
        }

        private void SeedQueue()
        {
            Enqueue("Morning News", "OBR", "Broadcast Blocks", "News", 2026, TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(15)), "Studio Feed", "Producer");
            Enqueue("Traffic Update", "OBR", "Broadcast Blocks", "News", 2026, TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(5)), "Traffic Desk", "Producer");
            Enqueue("Weather", "OBR", "Broadcast Blocks", "News", 2026, TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(45)), "Weather Center", "Producer");
            Enqueue("Drive Time Mix", "DJ Alex", "Rush Hour", "Mix", 2023, TimeSpan.FromMinutes(15), "Music Library", "DJ Alex");
            Enqueue("Late Night Chill", "FM Skyline", "Afterhours", "Synth", 2024, TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(12)), "Music Library", "Scheduler");
        }

        private void SeedChat()
        {
            AppendChatMessage("DJNova", "Spin that graphite groove!", false, false, false, DateTime.UtcNow);
            AppendChatMessage("StudioBot", "Queue sounds tight today.", true, false, true, DateTime.UtcNow);
            AppendChatMessage("Listener88", "Shoutout from Seattle!", false, false, false, DateTime.UtcNow);
        }

        private void InitializeCartWall()
        {
            foreach (var pad in _cartWallService.Pads)
            {
                pad.PropertyChanged += OnCartPadPropertyChanged;
            }

            SelectedCart = _cartWallService.Pads.FirstOrDefault();
            UpdateCartStatus(SelectedCart);
        }

        private void ToggleCartPad(CartPad? pad)
        {
            if (pad == null)
            {
                CartEditorStatus = "Select a cart slot before triggering.";
                return;
            }

            if (!pad.IsPlayable)
            {
                CartEditorStatus = "Assign an audio file before triggering this cart.";
                return;
            }

            if (SelectedCart != pad)
            {
                SelectedCart = pad;
            }

            _cartWallService.TogglePad(pad.Id);
            UpdateCartStatus(pad);
        }

        private void AssignCartPadFromPicker(CartPad? pad)
        {
            if (pad == null)
            {
                return;
            }

            if (_cartWallService.TryAssignPadFromFilePicker(pad.Id))
            {
                if (SelectedCart != pad)
                {
                    SelectedCart = pad;
                }

                UpdateCartStatus(pad);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void OnCartPadPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not CartPad pad)
            {
                return;
            }

            if (e.PropertyName == nameof(CartPad.FilePath)
                || e.PropertyName == nameof(CartPad.Label)
                || e.PropertyName == nameof(CartPad.IsPlaying)
                || e.PropertyName == nameof(CartPad.IsPlayable)
                || e.PropertyName == nameof(CartPad.LoopEnabled))
            {
                if (pad == SelectedCart)
                {
                    UpdateCartStatus(pad);
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        private static IReadOnlyList<string> BuildCartColorPalette()
        {
            return new List<string>
            {
                "#FF2F3B52",
                "#FF1E2633",
                "#FF23303F",
                "#FF1A2432",
                "#FF2A3748",
                "#FF1C2836",
                "#FF2E3B4B",
                "#FF1F2936",
                "#FF2B3746",
                "#FF1D2633",
                "#FF2C3948",
                "#FF19212E"
            };
        }

        private void BrowseCartFile()
        {
            if (!HasSelectedCart || SelectedCart == null)
            {
                UpdateCartStatus(null);
                return;
            }

            if (_cartWallService.TryAssignPadFromFilePicker(SelectedCart.Id))
            {
                UpdateCartStatus(SelectedCart);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void PersistCartPads()
        {
            CartEditorStatus = "Cart bank saved to settings.";
            _cartWallService.SavePads();
        }

        private void UpdateCartStatus(CartPad? cart)
        {
            if (cart == null)
            {
                CartEditorStatus = "Select a cart slot to edit.";
                return;
            }

            if (!cart.HasAudio)
            {
                CartEditorStatus = $"Assign audio for {cart.Label}.";
                return;
            }

            if (!File.Exists(cart.FilePath))
            {
                CartEditorStatus = "That cart file cannot be found. Update the assignment.";
                return;
            }

            var fileName = Path.GetFileName(cart.FilePath);
            CartEditorStatus = cart.IsPlaying
                ? $"Playing {fileName}"
                : $"Cart ready: {fileName}";
        }

        private void Enqueue(string title, string artist, string album, string genre, int year, TimeSpan duration, string source, string requestedBy)
        {
            var track = new Track(title, artist, album, genre, year, duration);
            var queueItem = new QueueItem(track, QueueSource.Manual, source, requestedBy);
            _queueService.Enqueue(queueItem);
        }

        private void SyncQueueFromService()
        {
            var snapshot = _queueService.Snapshot();
            var viewModels = snapshot
                .Select(item => new QueueItemViewModel(item))
                .ToList();

            SyncCollection(QueueItems, viewModels);
            UpdateNextQueuePreview(snapshot.Count > 0 ? snapshot[0] : null);
            UpdateTwitchRequestSummary(snapshot);
            CommandManager.InvalidateRequerySuggested();
        }

        private void SyncQueueHistoryFromService()
        {
            var history = _queueService.HistorySnapshot()
                .Select(item => new QueueItemViewModel(item))
                .ToList();

            SyncCollection(QueueHistoryItems, history);
        }

        private void UpdateNextQueuePreview(QueueItem? next)
        {
            if (next == null)
            {
                NextQueueTitle = "Queue empty.";
                NextQueueArtist = "—";
                NextQueueAttribution = "Add tracks to preview upcoming play.";
                return;
            }

            NextQueueTitle = next.Track.Title;
            NextQueueArtist = next.Track.Artist;
            NextQueueAttribution = string.IsNullOrWhiteSpace(next.RequestAttribution)
                ? $"Source: {next.Source}"
                : next.RequestAttribution;
        }

        private void UpdateTwitchRequestSummary(IReadOnlyList<QueueItem> snapshot)
        {
            var pending = snapshot.Count(item => item.SourceType == QueueSource.Twitch);
            TwitchRequestSummary = pending switch
            {
                0 => "No pending Twitch requests.",
                1 => "1 pending Twitch request.",
                _ => $"{pending} pending Twitch requests."
            };
        }

        private void OnQueueServiceChanged(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                SyncQueueFromService();
                SyncAutoDjPreview();
            });
        }

        private void OnQueueHistoryChanged(object? sender, EventArgs e)
        {
            RunOnUiThread(SyncQueueHistoryFromService);
        }

        private void RemoveSelectedQueueItem()
        {
            var index = GetSelectedQueueIndex();
            if (index >= 0)
            {
                _queueService.RemoveAt(index);
            }
        }

        private void ClearQueue()
        {
            _queueService.Clear();
        }

        private void ShuffleQueue()
        {
            var snapshot = _queueService.Snapshot();
            if (snapshot.Count <= 1) return;

            var shuffled = snapshot.OrderBy(_ => Guid.NewGuid()).ToList();
            _queueService.Clear();
            foreach (var item in shuffled)
            {
                _queueService.Enqueue(item);
            }
        }

        private void MoveSelectedQueueItemToTop()
        {
            var index = GetSelectedQueueIndex();
            if (index > 0)
            {
                _queueService.Reorder(index, 0);
            }
        }

        private void MoveSelectedQueueItemToBottom()
        {
            var index = GetSelectedQueueIndex();
            var lastIndex = QueueItems.Count - 1;
            if (index >= 0 && index < lastIndex)
            {
                _queueService.Reorder(index, lastIndex);
            }
        }

        private bool CanMoveQueueItemUp()
        {
            return GetSelectedQueueIndex() > 0;
        }

        private bool CanMoveQueueItemDown()
        {
            var index = GetSelectedQueueIndex();
            return index >= 0 && index < QueueItems.Count - 1;
        }

        private int GetSelectedQueueIndex()
        {
            if (SelectedQueueItem == null)
            {
                return -1;
            }

            return QueueItems.IndexOf(SelectedQueueItem);
        }

        public void ReorderQueueItem(int fromIndex, int toIndex)
        {
            if (_queueService.Reorder(fromIndex, toIndex))
            {
                SyncQueueFromService();
            }
        }

        private void PrimeDecks()
        {
            _transportService.RequestNextFromQueue(DeckIdentifier.A);
            _transportService.RequestNextFromQueue(DeckIdentifier.B);
        }

        private async Task StartTwitchBridgeAsync()
        {
            if (_isTwitchConnecting)
            {
                return;
            }

            _isTwitchConnecting = true;
            TwitchStatusMessage = "Connecting to Twitch chat...";

            _twitchCts?.Cancel();
            _twitchCts?.Dispose();
            _twitchCts = new CancellationTokenSource();

            try
            {
                if (!IsTwitchSettingsValid(_twitchSettings))
                {
                    TwitchStatusMessage = "Open Twitch settings to configure username, token, and channel.";
                    ForceDisableTwitchToggle();
                    return;
                }

                var options = _twitchSettings.ToChatOptions();
                await _twitchService.StartAsync(_twitchSettings, _twitchCts.Token);
                TwitchStatusMessage = $"Connected to #{options.NormalizedChannel}.";
            }
            catch (OperationCanceledException)
            {
                TwitchStatusMessage = "Twitch chat connection canceled.";
            }
            catch (Exception ex)
            {
                TwitchStatusMessage = $"Twitch connect failed: {ex.Message}";
                ForceDisableTwitchToggle();
            }
            finally
            {
                _isTwitchConnecting = false;
            }
        }

        private void StopTwitchBridge()
        {
            _twitchCts?.Cancel();
            _twitchCts?.Dispose();
            _twitchCts = null;
            TwitchStatusMessage = "Twitch chat offline.";
            _ = _twitchService.StopAsync();
        }

        private void ForceDisableTwitchToggle()
        {
            if (_twitchChatEnabled)
            {
                _suppressTwitchToggle = true;
                SetProperty(ref _twitchChatEnabled, false, nameof(TwitchChatEnabled));
                _suppressTwitchToggle = false;
            }

            StopTwitchBridge();
        }

        private void OpenTwitchSettingsDialog()
        {
            var settingsVm = new TwitchSettingsViewModel(_twitchSettings.Clone());
            var window = new TwitchSettingsWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = settingsVm
            };

            var result = window.ShowDialog();
            if (result != true)
            {
                return;
            }

            _twitchSettings = settingsVm.ToSettings();
            _twitchSettingsStore.Save(_twitchSettings);
            _twitchService.UpdateSettings(_twitchSettings);

            if (TwitchChatEnabled)
            {
                StopTwitchBridge();
                _ = StartTwitchBridgeAsync();
            }
        }

        private void OpenManageCategoriesDialog()
        {
            var window = new ManageCategoriesWindow(_libraryService)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.ShowDialog();
            RefreshCategorySnapshot();
        }

        private void RemoveUncategorizedTracks()
        {
            var allTracks = _libraryService.GetTracks();
            var uncategorized = allTracks.Where(t => t.CategoryIds.Count == 0).ToList();
            
            if (uncategorized.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No uncategorized tracks found.",
                    "Remove Uncategorized",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Remove {uncategorized.Count} uncategorized track(s) from the library?\\n\\nThis will permanently delete tracks that have no categories assigned.",
                "Confirm Remove Uncategorized",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var track in uncategorized)
                {
                    _libraryService.RemoveTrack(track.Id);
                }
                System.Windows.MessageBox.Show(
                    $"Removed {uncategorized.Count} uncategorized track(s).",
                    "Remove Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void CleanupReservedCategories()
        {
            var allCategories = _libraryService.GetCategories();
            var reserved = new[] { "All Categories", "All", "Uncategorized" };
            var toRemove = allCategories
                .Where(c => reserved.Any(r => string.Equals(r, c.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            if (toRemove.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No reserved category names found in the database.",
                    "Cleanup Reserved Names",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var names = string.Join(", ", toRemove.Select(c => $"'{c.Name}'"));
            var result = System.Windows.MessageBox.Show(
                $"Found {toRemove.Count} reserved category name(s): {names}\n\nRemove these from the database? Tracks assigned to these categories will keep their other categories.",
                "Confirm Cleanup",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var category in toRemove)
                {
                    _libraryService.RemoveCategory(category.Id);
                }
                System.Windows.MessageBox.Show(
                    $"Removed {toRemove.Count} reserved category name(s).",
                    "Cleanup Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void OpenAppSettingsDialog()
        {
            var playbackDevices = _audioService.GetOutputDevices();
            var inputDevices = _audioService.GetInputDevices();
            List<LibraryCategory> categories;
            lock (_libraryCacheLock)
            {
                categories = _libraryCategoryLookup.Values.ToList();
            }

            var settingsVm = new SettingsViewModel(_appSettings, playbackDevices, inputDevices, categories);
            var window = new SettingsWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = settingsVm
            };

            EventHandler<AppSettings>? handler = null;
            handler = (_, updated) =>
            {
                _appSettings = updated ?? new AppSettings();
                ApplyAppSettings(_appSettings);
            };

            settingsVm.SettingsChanged += handler;
            window.ShowDialog();
            settingsVm.SettingsChanged -= handler;
        }

        private void ApplyAppSettings(AppSettings? settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.ApplyDefaults();
            _appSettingsStore.Save(settings);
            _audioService.ApplyAudioSettings(settings.Audio);
            if (settings.Audio != null)
            {
                var deckAVolume = Math.Clamp(settings.Audio.DeckAVolumePercent, 0, 100);
                var deckBVolume = Math.Clamp(settings.Audio.DeckBVolumePercent, 0, 100);
                _suppressDeckVolumePersistence = true;
                DeckA?.SyncVolumeFromEngine(deckAVolume);
                DeckB?.SyncVolumeFromEngine(deckBVolume);
                _suppressDeckVolumePersistence = false;
            }
            UpdateEncoderConfiguration(settings);
            _overlayService.UpdateSettings(settings.Overlay);
            ApplyRequestSettings(settings.Requests);
            ApplyQueueSettings(settings.Queue);
            ApplyAutomationSettings(settings.Automation);

            if (settings.Twitch != null)
            {
                _twitchSettings = settings.Twitch.Clone();
                _twitchSettingsStore.Save(_twitchSettings);
                _twitchService.UpdateSettings(_twitchSettings);

                if (TwitchChatEnabled)
                {
                    StopTwitchBridge();
                    _ = StartTwitchBridgeAsync();
                }
            }
        }

        private void ApplyQueueSettings(QueueSettings? settings)
        {
            var limit = settings?.MaxHistoryItems ?? 5;
            _queueService.UpdateHistoryLimit(limit);
        }

        private void ApplyAutomationSettings(AutomationSettings? settings)
        {
            if (settings == null)
            {
                // Legacy: _autoDjController.TargetQueueDepth = 5; (removed)
                _clockwheelConfigured = false;
                EnsureClockwheelConfigured();
                _tohSchedulerService.UpdateSettings(new TohSettings());
                return;
            }

            var targetDepth = Math.Clamp(settings.TargetQueueDepth, 2, 20);
            // Legacy: _autoDjController.TargetQueueDepth = targetDepth; (removed)
            LoadClockwheelFromSettings(settings);

            // Update TOH settings
            _tohSchedulerService.UpdateSettings(settings.TopOfHour ?? new TohSettings());

            if (settings.AutoStartAutoDj && !_autoDjEnabled)
            {
                AutoDjEnabled = true;
            }
        }

        private void LoadClockwheelFromSettings(AutomationSettings? settings)
        {
            if (settings == null)
            {
                _clockwheelConfigured = false;
                EnsureClockwheelConfigured();
                return;
            }

            var rotationPrograms = BuildRotationPrograms(settings.Rotations);
            if (rotationPrograms.Count > 0)
            {
                var schedule = BuildRotationSchedule(settings.RotationSchedule);
                var defaultRotation = string.IsNullOrWhiteSpace(settings.DefaultRotationName)
                    ? rotationPrograms[0].Name
                    : settings.DefaultRotationName;
                // Legacy: _clockwheelScheduler.ConfigureRotations(rotationPrograms, schedule, defaultRotation); (removed)
                _clockwheelConfigured = true;
                return;
            }

            var materialized = MaterializeClockwheelSlots(settings.ClockwheelSlots);
            if (materialized.Count == 0)
            {
                _clockwheelConfigured = false;
                EnsureClockwheelConfigured();
                return;
            }

            // Legacy: _clockwheelScheduler.LoadSlots(materialized); (removed)
            _clockwheelConfigured = true;
        }

        private static List<ClockwheelSlot> MaterializeClockwheelSlots(IEnumerable<ClockwheelSlotSettings>? slots)
        {
            var materialized = new List<ClockwheelSlot>();
            if (slots == null)
            {
                return materialized;
            }

            foreach (var slotSettings in slots)
            {
                if (slotSettings == null)
                {
                    continue;
                }

                var hasCategory = !string.IsNullOrWhiteSpace(slotSettings.CategoryName);
                var hasTrack = Guid.TryParse(slotSettings.TrackId, out var parsedTrackId) && parsedTrackId != Guid.Empty;

                if (!hasCategory && !hasTrack)
                {
                    continue;
                }

                materialized.Add(slotSettings.ToSlot());
            }

            return materialized;
        }

        private static List<RotationProgram> BuildRotationPrograms(IEnumerable<RotationDefinitionSettings>? definitions)
        {
            var results = new List<RotationProgram>();
            if (definitions == null)
            {
                return results;
            }

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                var slots = MaterializeClockwheelSlots(definition.Slots);
                if (slots.Count == 0)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(definition.Name)
                    ? $"Rotation {results.Count + 1}"
                    : definition.Name.Trim();

                results.Add(new RotationProgram(name, slots));
            }

            return results;
        }

        private static List<RotationScheduleEntry> BuildRotationSchedule(IEnumerable<RotationScheduleEntrySettings>? entries)
        {
            var schedule = new List<RotationScheduleEntry>();
            if (entries == null)
            {
                return schedule;
            }

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.RotationName))
                {
                    continue;
                }

                if (!TimeSpan.TryParse(entry.StartTime, out var startTime))
                {
                    continue;
                }

                schedule.Add(new RotationScheduleEntry(entry.Day, startTime, entry.RotationName));
            }

            return schedule;
        }

        private void ApplyRequestSettings(RequestSettings? settings)
        {
            _twitchService.UpdateRequestSettings(settings ?? new RequestSettings());
        }

        private void UpdateEncoderConfiguration(AppSettings? settings)
        {
            if (settings?.Encoder == null || settings.Audio == null)
            {
                return;
            }

            _encoderManager.UpdateConfiguration(settings.Encoder, settings.Audio.EncoderDeviceId);
            SyncEncoderStatuses(_encoderManager.SnapshotStatuses());

            var hasEnabledProfiles = settings.Encoder.Profiles.Any(profile => profile.Enabled);
            if (!_encodersEnabled && settings.Encoder.AutoStart && hasEnabledProfiles)
            {
                EncodersEnabled = true;
            }
            else if (!hasEnabledProfiles)
            {
                ForceDisableEncoders();
            }
            else
            {
                UpdateEncoderStatusSummary();
            }
        }

        private void StartEncoders()
        {
            try
            {
                _encoderManager.Start();
                UpdateEncoderStatusSummary();
            }
            catch (Exception ex)
            {
                EncoderStatusMessage = $"Encoder start failed: {ex.Message}";
                ForceDisableEncoders();
            }
        }

        private void StopEncoders()
        {
            _encoderManager.Stop();
            UpdateEncoderStatusSummary();
        }

        private void ForceDisableEncoders()
        {
            if (!_encodersEnabled)
            {
                return;
            }

            _suppressEncoderToggle = true;
            SetProperty(ref _encodersEnabled, false, nameof(EncodersEnabled));
            _suppressEncoderToggle = false;
            StopEncoders();
        }

        private void OnDeckStateChangedForEncoderMetadata(DeckStateChangedEvent payload)
        {
            // Check if this is a new track starting to play
            var currentTrackId = payload.QueueItem?.Track?.Id;
            
            if (payload.DeckId == DeckIdentifier.A)
            {
                var lastTrackId = _lastAnnouncedDeckATrackId;
                _deckAState = payload;
                
                // Announce all new tracks to Twitch chat
                if (payload.IsPlaying && currentTrackId.HasValue && currentTrackId != lastTrackId)
                {
                    _lastAnnouncedDeckATrackId = currentTrackId;
                    _twitchService.AnnounceNowPlaying(payload.QueueItem);
                }
            }
            else if (payload.DeckId == DeckIdentifier.B)
            {
                var lastTrackId = _lastAnnouncedDeckBTrackId;
                _deckBState = payload;
                
                // Announce all new tracks to Twitch chat
                if (payload.IsPlaying && currentTrackId.HasValue && currentTrackId != lastTrackId)
                {
                    _lastAnnouncedDeckBTrackId = currentTrackId;
                    _twitchService.AnnounceNowPlaying(payload.QueueItem);
                }
            }

            UpdateEncoderMetadataFromDecks();
        }

        private void UpdateEncoderMetadataFromDecks()
        {
            var candidate = SelectEncoderDeckCandidate();
            var track = candidate?.QueueItem?.Track;
            _encoderManager.UpdateNowPlayingMetadata(track);
        }

        private DeckStateChangedEvent? SelectEncoderDeckCandidate()
        {
            var states = new[] { _deckAState, _deckBState };
            var playing = states
                .Where(state => state?.QueueItem?.Track != null && state.IsPlaying)
                .OrderBy(state => state!.DeckId)
                .FirstOrDefault();

            if (playing != null)
            {
                return playing;
            }

            return states
                .Where(state => state?.QueueItem?.Track != null)
                .OrderBy(state => state!.DeckId)
                .FirstOrDefault();
        }

        private void OnEncoderStatusChanged(object? sender, EncoderStatusChangedEventArgs e)
        {
            if (e?.Status == null)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                ApplyEncoderStatus(e.Status);
                UpdateEncoderStatusSummary();
            });
        }

        private void ApplyEncoderStatus(EncoderStatus status)
        {
            var vm = EncoderStatuses.FirstOrDefault(item => item.ProfileId == status.ProfileId);
            if (vm == null)
            {
                vm = new EncoderStatusViewModel(status.ProfileId, status.Name);
                EncoderStatuses.Add(vm);
            }

            vm.Update(status);
        }

        private void SyncEncoderStatuses(IEnumerable<EncoderStatus> statuses)
        {
            var snapshot = statuses?.ToList() ?? new List<EncoderStatus>();
            RunOnUiThread(() =>
            {
                var ids = snapshot.Select(status => status.ProfileId).ToHashSet();
                for (var i = EncoderStatuses.Count - 1; i >= 0; i--)
                {
                    if (!ids.Contains(EncoderStatuses[i].ProfileId))
                    {
                        EncoderStatuses.RemoveAt(i);
                    }
                }

                foreach (var status in snapshot)
                {
                    ApplyEncoderStatus(status);
                }

                UpdateEncoderStatusSummary();
            });
        }

        private void UpdateEncoderStatusSummary()
        {
            if (EncoderStatuses.Count == 0)
            {
                EncoderStatusMessage = "No encoder profiles configured.";
                return;
            }

            var streamingCount = EncoderStatuses.Count(status => status.State == EncoderState.Streaming);
            if (streamingCount > 0)
            {
                EncoderStatusMessage = streamingCount == 1
                    ? "Streaming to 1 target."
                    : $"Streaming to {streamingCount} targets.";
                return;
            }

            if (EncoderStatuses.Any(status => status.State == EncoderState.Connecting))
            {
                EncoderStatusMessage = "Encoders connecting...";
                return;
            }

            var failed = EncoderStatuses.FirstOrDefault(status => status.State == EncoderState.Failed);
            if (failed != null)
            {
                EncoderStatusMessage = $"Encoder error: {failed.Message}";
                return;
            }

            EncoderStatusMessage = _encodersEnabled
                ? "Encoders armed."
                : "Encoders offline.";
        }

        private void OnTwitchChatMessage(object? sender, TwitchChatMessage message)
        {
            RunOnUiThread(() =>
            {
                AppendChatMessage(message.UserName, message.Message, message.IsSystem, message.IsFromBroadcaster, message.IsFromBot, message.TimestampUtc);
            });
        }

        private void AppendChatMessage(string userName, string message, bool isSystem, bool isBroadcaster, bool isBot, DateTime? timestampUtc = null)
        {
            var chat = new TwitchChatMessageViewModel
            {
                UserName = userName,
                Message = message,
                Timestamp = FormatTimestamp(timestampUtc ?? DateTime.UtcNow),
                IsSystem = isSystem,
                IsFromBroadcaster = isBroadcaster,
                IsFromBot = isBot
            };

            ChatMessages.Add(chat);
            TrimChatHistory();
        }

        private void TrimChatHistory()
        {
            while (ChatMessages.Count > ChatHistoryLimit)
            {
                ChatMessages.RemoveAt(0);
            }
        }

        private static string FormatTimestamp(DateTime timestampUtc)
        {
            return timestampUtc.ToLocalTime().ToString("HH:mm");
        }

        private static void RunOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                // Use BeginInvoke (async) instead of Invoke (sync) to prevent deadlocks
                dispatcher.BeginInvoke(action);
            }
        }

        private double ApplyDeckVolume(DeckIdentifier deckId, double requestedPercent)
        {
            var clampedPercent = Math.Clamp(requestedPercent, 0d, 100d);
            var appliedScalar = _audioService.SetDeckVolume(deckId, clampedPercent / 100d);
            var appliedPercent = Math.Round(appliedScalar * 100d);

            if (!_suppressDeckVolumePersistence && _appSettings?.Audio != null)
            {
                if (deckId == DeckIdentifier.A)
                {
                    _appSettings.Audio.DeckAVolumePercent = (int)appliedPercent;
                }
                else
                {
                    _appSettings.Audio.DeckBVolumePercent = (int)appliedPercent;
                }

                _appSettingsStore.Save(_appSettings);
            }

            return appliedPercent;
        }

        private static bool IsTwitchSettingsValid(TwitchSettings settings)
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
            _clockTimer.Stop();
            ForceDisableEncoders();
            StopTwitchBridge();
            _twitchCts?.Dispose();
            _twitchService.Dispose();
            _tohSchedulerService.Dispose();
            _transportService.Dispose();
            _audioService.VuMetersUpdated -= OnVuMetersUpdated;
            _audioService.Dispose();
            _queueService.QueueChanged -= OnQueueServiceChanged;
            _queueService.HistoryChanged -= OnQueueHistoryChanged;
            _libraryService.TracksChanged -= OnLibraryTracksChanged;
            _libraryService.CategoriesChanged -= OnLibraryCategoriesChanged;
            _encoderMetadataSubscription.Dispose();
            foreach (var pad in _cartWallService.Pads)
            {
                pad.PropertyChanged -= OnCartPadPropertyChanged;
            }
            _encoderManager.StatusChanged -= OnEncoderStatusChanged;
            _encoderManager.Dispose();
            _sharedEncoderSource.Dispose();
            _overlayService.Dispose();
        }
    }

    public class DeckViewModel : ViewModelBase
    {
        private readonly DeckIdentifier _deckId;
        private readonly TransportService _transportService;
        private readonly IDisposable _subscription;
        private readonly Func<double, double>? _applyVolume;
        private bool _suppressVolumeUpdates;
        private double _volumePercent = 100;

        public DeckViewModel(string name, DeckIdentifier deckId, TransportService transportService, IEventBus eventBus, Func<double, double>? applyVolume = null, double initialVolumePercent = 100)
        {
            Name = name;
            _deckId = deckId;
            _transportService = transportService ?? throw new ArgumentNullException(nameof(transportService));
            _subscription = eventBus?.Subscribe<DeckStateChangedEvent>(OnDeckStateChanged)
                ?? throw new ArgumentNullException(nameof(eventBus));
            _applyVolume = applyVolume;

            PlayCommand = new RelayCommand(_ => _transportService.Play(_deckId));
            StopCommand = new RelayCommand(_ => _transportService.Stop(_deckId));
            NextCommand = new RelayCommand(_ => LoadNextFromQueue());

            Title = $"{name} Ready";
            Artist = "—";
            Album = "—";
            Year = "—";
            ElapsedTime = FormatTime(TimeSpan.Zero);
            RemainingTime = "--:--";

            VolumePercent = initialVolumePercent;
        }

        public string Name { get; }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        private string _artist = string.Empty;
        public string Artist
        {
            get => _artist;
            private set => SetProperty(ref _artist, value);
        }

        private string _album = string.Empty;
        public string Album
        {
            get => _album;
            private set => SetProperty(ref _album, value);
        }

        private string _year = string.Empty;
        public string Year
        {
            get => _year;
            private set => SetProperty(ref _year, value);
        }

        private System.Windows.Media.Imaging.BitmapImage? _albumArt;
        public System.Windows.Media.Imaging.BitmapImage? AlbumArt
        {
            get => _albumArt;
            private set => SetProperty(ref _albumArt, value);
        }

        private string _elapsedTime = "00:00";
        public string ElapsedTime
        {
            get => _elapsedTime;
            private set => SetProperty(ref _elapsedTime, value);
        }

        private string _remainingTime = "--:--";
        public string RemainingTime
        {
            get => _remainingTime;
            private set => SetProperty(ref _remainingTime, value);
        }

        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand NextCommand { get; }

        public double VolumePercent
        {
            get => _volumePercent;
            set
            {
                var requested = Math.Clamp(value, 0d, 100d);
                var applied = requested;

                if (!_suppressVolumeUpdates && _applyVolume != null)
                {
                    applied = Math.Clamp(_applyVolume(requested), 0d, 100d);
                }

                if (SetProperty(ref _volumePercent, applied))
                {
                    OnPropertyChanged(nameof(VolumePercentDisplay));
                }
            }
        }

        public string VolumePercentDisplay => $"{Math.Round(VolumePercent)}%";

        public void SyncVolumeFromEngine(double percent)
        {
            _suppressVolumeUpdates = true;
            try
            {
                VolumePercent = percent;
            }
            finally
            {
                _suppressVolumeUpdates = false;
            }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    PulseBorderActive = value;
                }
            }
        }

        private bool _pulseBorderActive;
        public bool PulseBorderActive
        {
            get => _pulseBorderActive;
            private set => SetProperty(ref _pulseBorderActive, value);
        }

        private void LoadNextFromQueue()
        {
            var next = _transportService.RequestNextFromQueue(_deckId);
            if (next == null)
            {
                _transportService.Stop(_deckId);
                return;
            }

            _transportService.Play(_deckId);
        }

        private void OnDeckStateChanged(DeckStateChangedEvent payload)
        {
            if (payload.DeckId != _deckId)
            {
                return;
            }

            ApplyMetadata(payload.QueueItem?.Track);
            ElapsedTime = FormatTime(payload.Elapsed);
            RemainingTime = FormatTime(payload.Remaining);
            IsPlaying = payload.Status == DeckStatus.Playing;
        }

        private void ApplyMetadata(Track? track)
        {
            if (track == null)
            {
                Title = $"{Name} Ready";
                Artist = "—";
                Album = "—";
                Year = "—";
                AlbumArt = null;
                return;
            }

            Title = track.Title;
            Artist = track.Artist;
            Album = track.Album;
            Year = track.Year.ToString();
            AlbumArt = AlbumArtExtractor.ExtractAlbumArt(track.FilePath);
        }

        private static string FormatTime(TimeSpan value)
        {
            return value < TimeSpan.Zero ? "--:--" : value.ToString(@"mm\:ss");
        }
    }

    public sealed class EncoderStatusViewModel : ViewModelBase
    {
        public EncoderStatusViewModel(Guid profileId, string name)
        {
            ProfileId = profileId;
            _name = name;
        }

        public Guid ProfileId { get; }

        private string _name;
        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }

        private EncoderState _state;
        public EncoderState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        private DateTimeOffset? _lastConnected;
        public DateTimeOffset? LastConnected
        {
            get => _lastConnected;
            private set
            {
                if (SetProperty(ref _lastConnected, value))
                {
                    OnPropertyChanged(nameof(LastConnectedDisplay));
                }
            }
        }

        public string LastConnectedDisplay => LastConnected?.ToLocalTime().ToString("HH:mm:ss") ?? "—";

        public void Update(EncoderStatus status)
        {
            if (status == null)
            {
                return;
            }

            Name = status.Name;
            State = status.State;
            Message = status.Message;
            LastConnected = status.LastConnected;
        }
    }

    public class QueueItemViewModel
    {
        public QueueItemViewModel(QueueItem item)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public QueueItem Item { get; }
        public string Title => Item.Track.Title;
        public string Artist => Item.Track.Artist;
        public string Duration => Item.Track.Duration.ToString(@"mm\:ss");
        public QueueSource SourceType => Item.SourceType;
        public string Source => string.IsNullOrWhiteSpace(Item.Source) ? Item.SourceType.ToString() : Item.Source;
        public string RequestedBy => Item.RequestedBy;
        public bool HasRequester => Item.HasRequester;
        
        /// <summary>
        /// True if this track was requested via Twitch chat
        /// </summary>
        public bool IsTwitchRequest => Item.SourceType == QueueSource.Twitch && Item.HasRequester;
        
        /// <summary>
        /// True if this track is a Top-of-the-Hour item
        /// </summary>
        public bool IsTopOfHour => Item.SourceType == QueueSource.TopOfHour;
        
        /// <summary>
        /// Display text for Twitch requests: "Requested by username"
        /// </summary>
        public string RequestedByDisplay => IsTwitchRequest ? $"Requested by {Item.RequestedBy}" : string.Empty;
        
        public string RequestSummary
        {
            get
            {
                // Don't show summary for Twitch requests (handled separately with green border)
                // and don't show for AutoDJ or TopOfHour (they show via Source label)
                if (IsTwitchRequest || Item.SourceType == QueueSource.AutoDj || Item.SourceType == QueueSource.TopOfHour)
                {
                    return string.Empty;
                }

                if (Item.HasRequester && !string.IsNullOrWhiteSpace(Item.RequestAttribution))
                {
                    return Item.RequestAttribution;
                }

                var label = Source;
                return string.IsNullOrWhiteSpace(label) ? string.Empty : $"Source: {label}";
            }
        }

        public bool HasRequestSummary => !string.IsNullOrWhiteSpace(RequestSummary);
        public string CreatedTimestamp => Item.CreatedAtUtc.ToLocalTime().ToString("HH:mm");
    }

    public sealed class AutoDjPreviewItemViewModel
    {
        public AutoDjPreviewItemViewModel(string category, string title, string artist)
        {
            Category = category;
            Title = title;
            Artist = artist;
        }

        public string Category { get; }
        public string Title { get; }
        public string Artist { get; }
    }

    public class SongLibraryItemViewModel
    {
        public Guid TrackId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Categories { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public IReadOnlyCollection<Guid> CategoryIds { get; set; } = Array.Empty<Guid>();
    }

    public sealed class LibraryCategoryOption
    {
        private LibraryCategoryOption(Guid categoryId, string displayName, string type, bool isAll)
        {
            CategoryId = categoryId;
            DisplayName = displayName;
            Type = type;
            IsAll = isAll;
        }

        public Guid CategoryId { get; }
        public string DisplayName { get; }
        public string Type { get; }
        public bool IsAll { get; }

        public static LibraryCategoryOption CreateAll()
        {
            return new LibraryCategoryOption(Guid.Empty, "All Categories", "All", true);
        }

        public static LibraryCategoryOption FromCategory(LibraryCategory category)
        {
            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            var label = string.IsNullOrWhiteSpace(category.Type)
                ? category.Name
                : $"{category.Name} ({category.Type})";
            return new LibraryCategoryOption(category.Id, label, category.Type, false);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class TwitchChatMessageViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsFromBroadcaster { get; set; }
        public bool IsFromBot { get; set; }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
