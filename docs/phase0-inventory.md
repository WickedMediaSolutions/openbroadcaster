Phase 0 Inventory
===================

Services
--------
| Component | Location | Responsibilities | Key Dependencies | Observations |
|-----------|----------|------------------|------------------|--------------|
| AppSettingsStore | Core/Services/AppSettingsStore.cs | Loads/saves `AppSettings` JSON, applies defaults, stores under %AppData%/OpenBroadcaster/settings.json. | `AppSettings` model, `System.Text.Json`. | No validation beyond `ApplyDefaults`; no concurrency guard but file access is serialized per call. |
| AudioService | Core/Services/AudioService.cs | Owns two `AudioDeck` instances (Deck A/B) plus `CartPlayer`, exposes device enumeration, deck cue/play/stop, cart playback. | `OpenBroadcaster.Core.Audio` (AudioDeck, CartPlayer), `NAudio`. | No logging or exception surfacing; routing only supports a single output per deck/cart. |
| CartPadStore | Core/Services/CartPadStore.cs | Persists cart pad label/path/color/loop to settings.json under `CartWall`. | `AppSettingsStore`, `CartPad`. | Migrates legacy `cartwall.json` on load; single source of truth for cart pads. |
| CartWallService | Core/Services/CartWallService.cs | Maintains observable pad collection, handles file assignment, toggled playback, persistence, uses SynchronizationContext. | `AudioService`, `CartPadStore`, `CartPad`. | Currently routes cart audio only via `AudioService.PlayCart`; no encoder/main bus separation. |
| LibraryService | Core/Services/LibraryService.cs | JSON-backed track & category store with CRUD, category validation, and concurrency locking. | `Track`, `LibraryCategory`. | Lacks actual media scanning/import; metadata limited to Track model fields. |
| LoyaltyLedger | Core/Services/LoyaltyLedger.cs | Persists Twitch loyalty/points ledger, add/debit APIs, thread-safe operations. | `System.Text.Json`. | No scheduled accrual or decay; file stored at base directory root (not AppData). |
| QueueService | Core/Services/QueueService.cs | Simple in-memory queue with enqueue/dequeue/peek/reorder and snapshot. | None beyond `QueueItem`. | No events/notifiers; no priority or source awareness. |
| TransportService | Core/Services/TransportService.cs | Manages two `Deck` objects, exposes load/play/pause/stop/unload, publishes `DeckStateChangedEvent` through `IEventBus`. | `IEventBus`, `Deck`, `DeckIdentifier`. | Deck timing/elapsed currently derived from `Deck` state only; no actual audio transport integration. |
| TwitchIntegrationService | Core/Services/TwitchIntegrationService.cs | Bridges Twitch IRC commands to queue/loyalty operations, awards points, exposes status/chat events. | `QueueService`, `TransportService`, `LoyaltyLedger`, `TwitchIrcClient`, `TwitchSettings`. | Command handling embedded here; AutoDJ, request search, and rotation integration not implemented. |
| TwitchIrcClient | Core/Services/TwitchIrcClient.cs | Minimal IRC socket client to connect/send/receive Twitch chat. | `TcpClient`, `StreamReader/Writer`. | No reconnection logic, TLS, or rate limiting; events only, no DI abstraction. |
| TwitchSettingsStore | Core/Services/TwitchSettingsStore.cs | Saves/loads Twitch settings JSON under base directory. | `TwitchSettings`. | Does not encrypt/obfuscate OAuth token. |

ViewModels
----------
| ViewModel | Location | Responsibilities | Bound Views / Commands | Observations |
|-----------|----------|------------------|------------------------|--------------|
| MainViewModel | ViewModels/MainViewModel.cs | Application shell: instantiates all core services, seeds demo data, owns deck view models, queue, library, cart wall, Twitch bridge toggles. | Bound to `MainWindow.xaml`; commands for Twitch settings/app settings; deck control commands internal to `DeckViewModel`. | Instantiates services directly (tight coupling, no DI). AutoDJ, rotation, audio routing, VU meters, encoder controls absent. |
| DeckViewModel | Nested in MainViewModel | Mirrors deck metadata, elapsed/remaining, exposes Play/Stop/Next commands via `TransportService`. | DataContext for deck panels in `MainWindow.xaml`. | Pulls queue items via delegate; no waveform/timer updates beyond events. |
| CartItemViewModel / QueueItemViewModel / SongLibraryItemViewModel / TwitchChatMessageViewModel | Nested in MainViewModel | Simple DTO-style view models for cart slots, queue entries, library rows, and chat log. | Bound to cart wall, queue list, library list, chat list within `MainWindow.xaml`. | Cart items trigger audio directly through `AudioService`; lacks per-pad routing or LED states. |
| SettingsViewModel | ViewModels/SettingsViewModel.cs | Wraps `AppSettings` editing with dirty tracking, Apply/Cancel/Reload commands, raises `SettingsChanged`. | DataContext for `Views/SettingsWindow.xaml`. | Uses JSON clone comparison; all settings editing done via nested binding to `Settings`. |
| TwitchSettingsViewModel | ViewModels/TwitchSettingsViewModel.cs | Simple binding wrapper for Twitch settings dialog with validation, Save command gating. | DataContext for `Views/TwitchSettingsWindow.xaml`. | Validation limited to presence of username/token/channel; costs constrained to >=0. |

XAML Views & Bindings
---------------------
| View | Location | Purpose | Primary Bindings / Notes |
|------|----------|---------|---------------------------|
| App.xaml | App.xaml | WPF application definition, merges `Themes/StudioTheme.xaml`, startup at `MainWindow`. | No additional resources beyond merged theme. |
| MainWindow | MainWindow.xaml / MainWindow.xaml.cs | Main live-assist surface: menu, library list, chat, decks, control rack, queue, cart wall. DataContext is `MainViewModel`. | Library/queue lists bind to observable collections; no drag-and-drop definitions; control rack toggles Twitch only; no AutoDJ, mic, encoder controls; VU meters absent. |
| SettingsWindow | Views/SettingsWindow.xaml /.cs | Unified settings dialog: general, audio, Twitch, queue, advanced tabs. | Binds deeply into `SettingsViewModel.Settings`; handles Apply/Cancel/Reload plus password syncing. Many stub settings not wired to runtime services. |
| TwitchSettingsWindow | Views/TwitchSettingsWindow.xaml /.cs | Lightweight Twitch configuration dialog launched from main view. | Binds to `TwitchSettingsViewModel`; Save button ensures `IsValid`. |
| StudioTheme | Themes/StudioTheme.xaml | Global resource dictionary for palette, typography, button styles, menus, etc. | Provides consistent graphite aesthetic; includes LED style placeholder but not used for live controls yet. |

Key Gaps Identified During Inventory
------------------------------------
- Services instantiated directly in `MainViewModel` with no DI container, making testing and replacement difficult.
- Library lacks ingestion/import, metadata parsing, category UI, drag-and-drop, and persistence beyond manual JSON editing.
- QueueService has no events or integration with rotation/clockwheel/autodj; historical tracking missing.
- Transport/audio path stops at stub deck objects; actual audio playback state is not tied to deck models, so elapsed/remaining are placeholders.
- Twitch integration handles only a subset of required commands and routes everything directly to the queue without AutoDJ or rotation guardrails.
- No encoder/shoutcast services, VU metering, mic controls, or OBS overlay endpoints exist yet.
