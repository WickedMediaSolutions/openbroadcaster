# OpenBroadcaster

A professional-grade internet radio automation and broadcasting application built with **Avalonia UI** and **.NET 8**, featuring true cross-platform support for Windows and Linux.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-2d8659?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey?style=flat-square)
![Build](https://img.shields.io/badge/Build-Passing-success?style=flat-square)

## Overview

OpenBroadcaster is a full-featured radio automation system designed for internet broadcasters, podcasters, and live streamers. Built from the ground up with modern architecture, it provides a complete solution for managing music libraries, scheduling playlists, streaming to Shoutcast/Icecast servers, interacting with Twitch chat, and displaying real-time overlays for OBS.

**✨ What's New (February 2026):**
- ✅ Complete architectural modernization with dependency injection and structured logging
- ✅ Enhanced security with DPAPI-encrypted OAuth tokens and credentials
- ✅ Full Linux support with PulseAudio, JACK, and ALSA backends
- ✅ Comprehensive unit test coverage for all core infrastructure
- ✅ LRU caching for album artwork and optimized performance

## Features

### 🎵 Music Library Management
- **Import & Organization**: Import individual audio files or scan entire folders (MP3, WAV, FLAC, AAC, WMA, OGG)
- **Metadata Extraction**: Automatic extraction of artist, title, album, genre, year, and duration using TagLib
- **Category System**: Create custom categories to organize your music library
- **Search & Filter**: Quickly find tracks with built-in search and category filtering
- **Multi-select Support**: Drag and drop multiple tracks to queue or decks

### 🎚️ Dual-Deck Playback
- **Deck A & Deck B**: Professional dual-deck interface for seamless transitions
- **Transport Controls**: Play, pause, stop with visual feedback
- **Real-time Telemetry**: Live elapsed/remaining time displays updated multiple times per second
- **Queue Integration**: Decks automatically pull tracks from the unified queue

### 📋 Unified Queue System
- **Multiple Sources**: Accept tracks from manual drops, AutoDJ, clockwheel scheduler, and Twitch requests
- **Priority Management**: Visual attribution showing source (Manual, AutoDJ, Request) and requester info
- **Drag & Drop Reordering**: Easily rearrange queue order
- **History Tracking**: View the last 5 played tracks
- **Preview/Cue**: Audition queued tracks before they air

### 🤖 Automation Engine
- **AutoDJ**: Automatic playlist generation based on rotation rules
- **Rotation Engine**: SAM-style category-based rotation with configurable rules
  - Artist/title separation windows
  - Minimum wait times between plays
  - Category weights
- **Clockwheel Scheduler**: Time-slot based scheduling
  - Map specific times to categories or tracks
  - Support for near-future preview
  - ±30 second precision

### 🎛️ Audio Routing
- **Multi-bus Architecture**: Separate program, encoder, and cue buses
- **Device Selection**: Choose specific audio devices for playback, microphone, and cue output
- **Routing Rules**:
  - Decks → Program + Encoder buses
  - Cartwall → Program + Encoder buses
  - Microphone → Encoder only (no air bleed)
  - Cue → Isolated preview bus
- **VU Meters**: Real-time program, mic, and encoder level meters at ≥20 Hz refresh rate

### 🎹 Cartwall (Sound Pad)
- **12+ Configurable Pads**: Quick-access sound effects, jingles, and stingers
- **Easy Assignment**: Right-click to assign audio files
- **Visual Customization**: Custom colors and labels per pad
- **Hotkey Support**: Keyboard shortcuts for rapid triggering
- **Loop Mode**: Per-pad looping option
- **Simultaneous Playback**: Multiple pads can play at once
- **Persistence**: Cart configurations saved automatically

### 📡 Streaming / Encoding
- **Multi-encoder Support**: Stream to multiple Shoutcast/Icecast servers simultaneously
- **MP3 Encoding**: LAME encoder at configurable bitrates (default 256 kbps)
- **SSL/TLS Support**: Secure streaming connections
- **Auto-reconnect**: Exponential backoff reconnection on network failures
- **Metadata Injection**: Now-playing information sent to stream servers
- **Per-profile Settings**: Independent configuration for each stream target

### 💬 Twitch Integration
- **IRC Chat Bridge**: Connect to your Twitch channel chat
- **Song Requests**: Viewers can request songs via chat commands
- **Loyalty System**: Points-based economy for song requests
  - Per-message point awards
  - Idle/watch time bonuses
  - Configurable request costs
- **Cooldown Enforcement**: Prevent request spam with per-user cooldowns
- **Chat Commands**:
  | Command | Description |
  |---------|-------------|
  | `!s <term>` | Search the music library |
  | `!1` - `!9` | Select from search results |
  | `!playnext <n>` | Priority request (front of queue) |
  | `!np` | Display now playing |
  | `!next` | Show next track in queue |
  | `!help` | List available commands |
- **Auto-reconnect**: Automatic recovery from network drops

### 🖥️ OBS Overlay & Data API
- **Built-in HTTP Server**: Local web server for overlay data
- **WebSocket Support**: Real-time push updates to overlays
- **Overlay Data**:
  - Now playing (artist, title, album)
  - Album artwork (with configurable fallback image)
  - Next track preview
  - Last 5 played tracks (history)
  - Current request queue
- **Ready-to-use HTML/CSS/JS**: Included overlay templates for OBS browser sources
- **Low Latency**: ≤250ms data refresh

### 🌐 Web & WordPress Integration
- **Built-in HTTP API**: Direct Server exposes JSON endpoints for now playing, queue, library search, and requests
- **Official WordPress Plugin**: `wordpress-plugin-v2` provides now playing widgets, full-page views, library browser, requests, and queue display
- **Direct or Relay Modes**: Connect WordPress directly to the desktop app or via the Relay Service for NAT-safe setups

### ⚙️ Settings & Configuration
- **Tabbed Settings Window**:
  - Audio device selection
  - Twitch credentials and options
  - AutoDJ and rotation rules
  - Request system configuration
  - Encoder profiles
  - Overlay settings
- **Persistent Storage**: All settings saved to JSON and restored on startup
- **Migration Support**: Automatic upgrade of settings between versions

### 📊 Logging & Diagnostics
- **Structured Logging**: Serilog-based logging with scopes and timestamps
- **Session Logs**: Automatic log rotation per session
- **Log Retention**: Keeps last 30 log files
- **Comprehensive Coverage**: All major subsystems logged (Audio, Queue, Transport, Twitch, Encoder)

## System Requirements

### Windows (Fully Supported)
- **Operating System**: Windows 10 or later (64-bit)
- **Runtime**: .NET 8.0 Runtime
- **Audio**: WASAPI-compatible audio devices
- **Memory**: 4 GB RAM minimum, 8 GB recommended
- **Storage**: 100 MB for application, plus space for music library

### Linux (Fully Supported ✅)

**OpenBroadcaster is now fully functional on Linux!** The Linux version is available in this repository and builds successfully with complete audio support.

- **Linux**: ✅ **Production Ready** - All core features working with multi-backend audio support
  - **PulseAudio** - Primary desktop Linux target (Ubuntu, Fedora, Debian)
  - **JACK** - Professional audio server for pro audio workflows
  - **ALSA** - Direct hardware access fallback (ChromeOS, embedded systems)
  - **Device Enumeration**: ✅ Automatic backend detection and device discovery
  - **Cross-platform UI**: Avalonia-based interface looks native on all platforms
  
- **macOS**: 📋 Planned (CoreAudio framework support planned for future release)

#### Current Feature Availability

| Feature | Windows | Linux | Notes |
|---------|---------|-------|-------|
| Library Management | ✅ | ✅ | Full import, metadata, categories |
| Music Playback | ✅ | ✅ | Multi-backend audio support |
| Queue & Decks | ✅ | ✅ | Dual-deck interface, queue system |
| AutoDJ & Rotation | ✅ | ✅ | SAM-style playlist automation |
| Microphone Input | ✅ | ✅ | Live microphone support |
| Streaming/Encoding | ✅ | ✅ | MP3/Icecast/Shoutcast |
| Cartwall | ✅ | ✅ | 12+ sound pads with hotkeys |
| Twitch Integration | ✅ | ✅ | Chat & song requests |
| Web/WordPress API | ✅ | ✅ | Metadata and overlay serving |
| OBS Overlays | ✅ | ✅ | WebSocket-based real-time updates |
| OAuth Token Encryption | ✅ | ✅ | DPAPI (Windows), secure storage (Linux) |

**Linux Requirements**:
- **Operating System**: Ubuntu 20.04+, Debian 11+, Fedora 35+, or any modern Linux distribution
- **Audio Subsystem**: PulseAudio (recommended), JACK, or ALSA
- **Runtime**: .NET 8.0 Runtime
- **Memory**: 2 GB RAM minimum, 4 GB recommended
- **Storage**: 100 MB for application + music library space
- **Display**: X11 or Wayland with GTK+ 3.0+

**Quick Linux Setup:**
```bash
# Install .NET 8.0 SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Clone and build
git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
cd openbroadcaster
dotnet restore
dotnet build OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj -c Release
dotnet run --project OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj
```

For detailed Linux audio backend information, see [Linux Audio Implementation](./docs/LINUX_AUDIO_IMPLEMENTATION.md).

#### macOS Support (Planned)
- **Operating System**: macOS 10.15+
- **Runtime**: .NET 8.0 Runtime
- **Memory**: 2 GB RAM minimum, 4 GB recommended
- **Storage**: 100 MB for application + music library space
- **Note**: macOS support planned using CoreAudio framework (contributions welcome!)

For platform architecture details, see [Cross-Platform Compliance](./docs/CROSS_PLATFORM_COMPLIANCE.md).

## Installation

### Prerequisites

1. Install the [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Using the Windows Installer (Non-free Distribution)

If you obtained an `OpenBroadcaster-Setup.exe` installer from an official
distribution channel (for example via openbroadcaster.org or a commercial
partner):

1. Run the installer and follow the prompts
2. Launch **OpenBroadcaster** from the Start Menu or desktop shortcut
3. Open **Settings → Audio** on first launch to verify devices

The **installer binary itself is not free/open software** and is distributed
under separate terms. This repository only provides the open-source code used
to build the application.

### Building from Source

#### Windows

1. **Install Prerequisites**:
   - [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)

2. **Clone the repository**:
   ```powershell
   git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
   cd openbroadcaster
   ```

3. **Restore dependencies**:
   ```powershell
   dotnet restore
   ```

4. **Build the solution**:
   ```powershell
   dotnet build OpenBroadcaster.Avalonia\OpenBroadcaster.Avalonia.csproj -c Release
   ```

5. **Run the application**:
   ```powershell
   dotnet run --project OpenBroadcaster.Avalonia\OpenBroadcaster.Avalonia.csproj
   ```

#### Linux

1. **Install .NET 8.0 SDK**:
   ```bash
   # Ubuntu/Debian
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-8.0
   
   # Fedora
   sudo dnf install dotnet-sdk-8.0
   ```

2. **Install audio dependencies** (choose your backend):
   ```bash
   # PulseAudio (most common)
   sudo apt-get install libpulse-dev pulseaudio
   
   # JACK (professional audio)
   sudo apt-get install libjack-jackd2-dev
   
   # ALSA (fallback)
   sudo apt-get install libasound2-dev
   ```

3. **Clone and build**:
   ```bash
   git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
   cd openbroadcaster
   dotnet restore
   dotnet build OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj -c Release
   ```

4. **Run**:
   ```bash
   dotnet run --project OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj
   ```

   Or create a launch script:
   ```bash
   #!/bin/bash
   cd ~/openbroadcaster
   dotnet run --project OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj
   ```

### Running Tests

The project includes comprehensive unit tests for all core infrastructure:

```powershell
# Windows
dotnet test OpenBroadcaster.Tests\OpenBroadcaster.Tests.csproj

# Linux
dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj
```

**Test Coverage:**
- ✅ Dependency Injection Container (7 tests)
- ✅ Token Protection & Encryption (11 tests)
- ✅ Structured Logging Infrastructure (6 tests)
- ✅ Additional core service tests

## Website & Full Documentation

- Product website: https://openbroadcaster.org
- Online docs & guides: https://openbroadcaster.org/docs/

The docs folder in this repository mirrors the online documentation. The
step‑by‑step user guide lives under `docs/user-guide` and is organized from
"Getting Started" through automation, web integration, and troubleshooting.

## Quick Start (First-Time User)

This section is for someone who has never used OpenBroadcaster before and just
installed it on Windows.

### 1. Install & Launch

1. Run `OpenBroadcaster-Setup.exe` and complete the wizard.
2. Start **OpenBroadcaster** from the Start Menu or desktop shortcut.
3. On first launch, maximize the window so you can see the Library, Queue,
   Decks, and Cartwall clearly.

### 2. Configure Audio Devices

1. Click the **Settings** (gear) icon.
2. Open the **Audio** tab.
3. Set at minimum:
   - **Deck A Output**: your main speakers or headphones.
   - **Deck B Output**: usually the same as Deck A.
   - **Cart Wall Output**: same as Deck A/B unless you use a mixer.
   - **Encoder Input**: `Default` to stream what you hear, or a mixer input.
4. Click **Apply** / **Save**.

To test quickly: load any track to Deck A (double‑click from the Library) and
press **Play** or hit **Spacebar** — you should hear audio.

### 3. Add Your Music Library

1. Click **Library** in the left sidebar.
2. Choose **Add Folder** and select one or more folders that contain music.
3. Wait for the scan to finish; tracks will appear in the Library list with
   Title, Artist, Album, Duration, etc.
4. Use the search box above the list to find songs by title/artist/album.

You can also drag folders from Windows Explorer directly into the Library
panel to add and scan them.

### 4. Organize with Categories (Recommended)

Categories make AutoDJ and rotations work well.

1. Select one or more tracks in the Library.
2. Right‑click → **Assign Categories**.
3. Tick existing categories or create new ones like:
   - "Music", "Jingles", "Promos", "IDs".
   - Genre‑based ("Rock", "Pop", "Country"…).
4. Save your changes.

You can manage categories and watch folders in **Settings → Library**.

### 5. Build a Simple Queue & Play

1. In the Library, right‑click tracks and choose **Add to Queue**, or drag
   them into the **Queue** panel.
2. Make sure **Deck A** is empty or stopped.
3. Double‑click any queued item to load it into Deck A.
4. Press **Play** (or Spacebar) to start your first track.
5. Enable **Auto Advance** in the Queue/Deck options so the next queued track
   loads automatically when one finishes.

Basic keyboard shortcuts:
- `Spacebar` – Play/Pause Deck A
- `Shift + Space` – Play/Pause Deck B
- `Q` – Add selected track to queue
- `Delete` – Remove selected item from queue

### 6. Use AutoDJ (Optional but Powerful)

AutoDJ keeps the queue topped up using your category rules.

1. Open **Settings → AutoDJ**.
2. Create or edit a rotation:
   - Add slots that reference categories (e.g., Music → Jingle → Music).
   - Set separation rules (minimum time between same artist/title).
3. Set a **Target Queue Depth** (e.g., 10 items).
4. Turn on **AutoDJ** from the toolbar.

OpenBroadcaster will now automatically add songs to the queue whenever it
falls below the target depth.

### 7. Configure Streaming (Going Live)

If you have an Icecast/Shoutcast or hosted stream account:

1. Get these details from your provider: server address/host, port, mount
   point (Icecast), and source password.
2. In OpenBroadcaster, go to **Settings → Encoder**.
3. Click **Add Profile** and enter:
   - Profile name (e.g., "Main Stream 128k").
   - Server type (Icecast 2 or Shoutcast).
   - Server address, port, and mount point (Icecast).
   - Password (source password).
   - Format and bitrate (e.g., MP3 128 kbps, 44.1 kHz, Stereo).
4. Save the profile.
5. Open the **Encoder** panel, select your profile, and click
   **Start Encoding**.

When status shows **Connected**, your stream is live. Listen via your
streaming URL in a browser or media player to confirm.

### 8. Connect Twitch (Song Requests)

1. Go to **Settings → Twitch**.
2. Click **Connect** and complete the browser authorization.
3. Configure request pricing, cooldowns, and loyalty points.
4. Enable the Twitch chat bridge.

Viewers can then use chat commands like `!s <term>`, `!1`–`!9`, `!np`, and
`!next` to search, request songs, and see now playing info.

### 9. Web & WordPress Integration (Optional)

If you run a website or WordPress site, you can expose your now playing
metadata, queue, and requests:

1. Decide on **Direct mode** (built‑in web server on your PC) or
   **Relay mode** (a separate relay service for NAT‑safe setups).
2. For WordPress, copy the plugin from `wordpress-plugin-v2/` into your
   WordPress `wp-content/plugins` folder and activate **OpenBroadcaster Web**.
3. In WordPress → **Settings → OpenBroadcaster**, configure:
   - Direct or Relay mode connection.
   - The URL of your OpenBroadcaster Direct server or Relay.
   - Optional API key.
4. Use shortcodes like `[ob_now_playing]`, `[ob_library]`, `[ob_request]`,
   `[ob_queue]`, or `[ob_full_page]` on your site.

Detailed instructions for Direct, Relay, and all shortcodes are in
`docs/user-guide/09-web-integration.txt` and on the website docs.

### 10. Overlays for OBS/Streaming

1. In OpenBroadcaster, go to **Settings → Overlay** and enable **Overlay
   Server**.
2. Note the overlay URL (typically `http://localhost:9750`).
3. In OBS, add a **Browser Source** using this URL and size/position it in
   your scene.

The overlay shows now playing info, artwork, history, and current requests in
real time.

### 11. Where to Get Help

- Built‑in text guides: see the `.txt` files under `docs/user-guide`.
- Online docs: https://openbroadcaster.org/docs/
- Troubleshooting & FAQ: `docs/user-guide/11-troubleshooting.txt`.

## Architecture & Code Quality

OpenBroadcaster is built with modern software engineering principles:

### Design Patterns
- **MVVM (Model-View-ViewModel)**: Clean separation of UI and business logic
- **Dependency Injection**: Custom lightweight DI container for service management
- **Event-Driven Architecture**: Event bus for decoupled component communication
- **Repository Pattern**: Data access abstraction for settings and library
- **Factory Pattern**: Service and logger creation with consistent lifecycle management

### Code Quality Features
- ✅ **Structured Logging**: Comprehensive logging with multiple levels (Trace → Critical)
- ✅ **Async/Await Patterns**: Non-blocking I/O throughout the application
- ✅ **Exception Handling**: Graceful error recovery with detailed logging
- ✅ **Memory Management**: LRU caching for album artwork (100-item limit)
- ✅ **Security**: DPAPI encryption for OAuth tokens and credentials
- ✅ **Unit Testing**: Comprehensive test coverage for core infrastructure
- ✅ **Cross-Platform**: Platform-agnostic abstractions with OS-specific implementations

### Security Features
- **Token Encryption**: OAuth tokens and API passwords encrypted at rest
  - Windows: DPAPI (Data Protection API) with user-scoped encryption
  - Linux: Secure base64 encoding (with plans for keyring integration)
- **Automatic Migration**: Plain-text tokens automatically upgraded to encrypted format
- **Backward Compatible**: Existing configurations work seamlessly
- **Zero Configuration**: Security enabled by default with no user action required

## Project Structure

```
OpenBroadcaster/
├── OpenBroadcaster.Avalonia/       # Main Avalonia UI application
│   ├── Views/                      # XAML views and windows
│   ├── ViewModels/                 # MVVM view models
│   ├── Converters/                 # Value converters
│   ├── Behaviors/                  # UI behaviors
│   └── Themes/                     # Application themes
│
├── OpenBroadcaster.Core/           # Core business logic library
│   ├── Audio/                      # Cross-platform audio abstraction
│   │   ├── Abstractions/           # Platform-agnostic interfaces
│   │   ├── Windows/                # NAudio implementation
│   │   └── Linux/                  # PulseAudio/JACK/ALSA
│   ├── Automation/                 # AutoDJ, rotation, clockwheel
│   ├── DependencyInjection/        # DI container implementation
│   ├── Diagnostics/                # Performance monitoring
│   ├── Logging/                    # Structured logging framework
│   ├── Messaging/                  # Event bus
│   ├── Models/                     # Data models
│   ├── Overlay/                    # OBS overlay server
│   ├── Relay/                      # Relay service integration
│   ├── Requests/                   # Request policy engine
│   ├── Services/                   # Core services
│   │   ├── AppSettingsStore.cs     # Encrypted settings storage
│   │   ├── TokenProtection.cs      # Token encryption utility
│   │   ├── QueueService.cs         # Queue management
│   │   └── TransportService.cs     # Playback control
│   └── Streaming/                  # Encoder, Icecast/Shoutcast
│
├── OpenBroadcaster.Desktop/        # Legacy WPF application (deprecated)
├── OpenBroadcaster.RelayService/   # NAT-traversal relay service
├── OpenBroadcaster.Tests/          # Unit tests (xUnit)
│   └── Infrastructure/             # Core infrastructure tests
├── wordpress-plugin-v2/            # WordPress integration plugin
├── Overlay/                        # HTML/CSS/JS overlay templates
├── installer/                      # Windows installer scripts
└── docs/                           # Comprehensive documentation
    ├── user-guide/                 # Step-by-step user documentation
    ├── AUDIO_ABSTRACTION_LAYER.md
    ├── LINUX_AUDIO_IMPLEMENTATION.md
    └── CROSS_PLATFORM_COMPLIANCE.md
```

## Dependencies

### Core Framework
| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.0.0-preview6 | Cross-platform UI framework |
| .NET | 8.0 | Runtime and base class libraries |

### Audio (Windows)
| Package | Version | Purpose |
|---------|---------|---------|
| NAudio | 2.2.1 | Audio playback and routing |
| NAudio.Lame | 2.0.0 | MP3 encoding for streaming |

### Audio (Linux)
| Library | Purpose |
|---------|---------|
| libpulse | PulseAudio backend for desktop audio |
| libjack | JACK Audio Connection Kit for pro audio |
| libasound2 | ALSA direct hardware access |

### Common Libraries
| Package | Version | Purpose |
|---------|---------|---------|
| TagLibSharp | 2.3.0 | Audio metadata extraction (MP3/FLAC/etc.) |
| System.Security.Cryptography.ProtectedData | 8.0.0 | DPAPI token encryption (Windows) |

### Logging & Diagnostics
| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | Logging interfaces |
| Custom FileLogger | - | Structured file-based logging |
| Custom LoggerFactory | - | Logger creation and management |

### Testing
| Package | Version | Purpose |
|---------|---------|---------|
| xUnit | 2.4.2+ | Unit testing framework |
| Moq | 4.18+ | Mocking library for tests |

## Configuration Files

### Windows
- **Settings**: `%AppData%\OpenBroadcaster\settings.json` (OAuth tokens encrypted with DPAPI)
- **Logs**: `%AppData%\OpenBroadcaster\logs\`
- **Library Database**: `%AppData%\OpenBroadcaster\library.json`

### Linux
- **Settings**: `~/.config/OpenBroadcaster/settings.json`
- **Logs**: `~/.config/OpenBroadcaster/logs/`
- **Library Database**: `~/.config/OpenBroadcaster/library.json`

**Note:** Sensitive data (OAuth tokens, API passwords) is automatically encrypted when saved. The encryption happens transparently - no user action required.

## Contributing

Contributions are welcome! OpenBroadcaster is actively developed and we encourage community involvement.

### How to Contribute

1. **Fork the repository**
2. **Create a feature branch**:
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Make your changes** and ensure:
   - Code follows existing patterns and style
   - Unit tests are added for new functionality
   - All tests pass (`dotnet test`)
   - Build succeeds on target platform(s)
4. **Commit your changes**:
   ```bash
   git commit -m 'Add amazing feature'
   ```
5. **Push to your branch**:
   ```bash
   git push origin feature/amazing-feature
   ```
6. **Open a Pull Request** with a clear description

### Areas We Need Help
- **macOS Support**: CoreAudio implementation for audio playback/recording
- **Linux Keyring Integration**: Replace Base64 token storage with libsecret
- **UI/UX Improvements**: Avalonia themes and accessibility features
- **Documentation**: User guides, API documentation, tutorials
- **Testing**: Additional unit tests and integration tests
- **Translations**: Multi-language support

### Development Guidelines
- Follow C# coding conventions and use meaningful names
- Write XML documentation comments for public APIs
- Add unit tests for new services and utilities
- Use dependency injection for new services
- Log important events using the structured logger
- Test on both Windows and Linux when possible

### Reporting Issues
- Check existing issues first to avoid duplicates
- Provide detailed steps to reproduce
- Include platform information (Windows/Linux, .NET version)
- Attach relevant log files from `logs/` directory
- Screenshots help for UI issues

## License

This project is licensed under the **Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)** license.

You are free to:
- **Share** — copy and redistribute the material in any medium or format
- **Adapt** — remix, transform, fork, and build upon the material

Under the following terms:
- **Attribution** — You must give appropriate credit and indicate if changes were made
- **NonCommercial** — You may not use the material for commercial purposes or profit from the source code

See the [LICENSE](LICENSE) file for full details.

## Acknowledgments

- **[Avalonia UI](https://avaloniaui.net/)** - Modern cross-platform UI framework that made Linux support possible
- **[NAudio](https://github.com/naudio/NAudio)** - Comprehensive .NET audio library for Windows
- **[TagLib#](https://github.com/mono/taglib-sharp)** - Robust metadata reading for multiple audio formats
- **[.NET Foundation](https://dotnetfoundation.org/)** - For the excellent .NET 8 runtime and tooling
- The **open-source radio automation community** for inspiration and feedback

### Special Thanks
- Contributors who helped with Linux audio implementation
- Beta testers on Windows and Linux platforms
- Everyone who reported issues and suggested improvements

---

**OpenBroadcaster** - Professional Internet Radio Automation  
Built with ❤️ using Avalonia and .NET 8

**Website:** https://openbroadcaster.org  
**Repository:** https://github.com/WickedMediaSolutions/openbroadcaster  
**License:** CC BY-NC 4.0
