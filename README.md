# OpenBroadcaster

A professional-grade internet radio automation and broadcasting application, built with Avalonia and .NET 8. Fully cross-platform with native support for Windows, Linux, and macOS.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-2d8659?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

## Overview

OpenBroadcaster is a full-featured radio automation system designed for internet broadcasters, podcasters, and live streamers. It provides a complete solution for managing music libraries, scheduling playlists, streaming to Shoutcast/Icecast servers, interacting with Twitch chat, and displaying real-time overlays for OBS.

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

### Linux / macOS (Expanding Cross-Platform Support)

#### Linux Audio Implementation Status

Audio support on Linux is currently being developed through a multi-backend architecture:

- **Windows**: ✅ **Complete** - Full audio playback, recording, and encoding (NAudio)
- **Linux**: 🚧 **In Development**
  - **PulseAudio** - Primary desktop Linux target (libpulse)
  - **JACK** - Professional audio server (libjack)
  - **ALSA** - Fallback for container-based systems like ChromeOS (libasound)
  - **Device Enumeration**: ✅ Working with automatic backend detection
  
- **macOS**: 📋 Future (CoreAudio framework)

#### Current Feature Availability

| Feature | Windows | Linux | macOS | Notes |
|---------|---------|-------|-------|-------|
| Library Management | ✅ | ✅ | 📋 | Full import, metadata, categories |
| Music Playback | ✅ | 🚧 | 📋 | Audio backend implementation in progress |
| Queue & Decks | ✅ | ✅ | 📋 | Full UI, playback queuing system |
| AutoDJ & Rotation | ✅ | ✅ | 📋 | Playlist automation |
| Microphone Input | ✅ | 🚧 | 📋 | Linux backend implementation in progress |
| Streaming/Encoding | ✅ | 🚧 | 📋 | Requires audio playback engine |
| Cartwall | ✅ | 🚧 | 📋 | Requires audio playback engine |
| Twitch Integration | ✅ | ✅ | 📋 | Chat & song requests |
| Web/WordPress API | ✅ | ✅ | 📋 | Metadata and overlay serving |

**LinuxRequirements** (Audio playback):
- **Operating System**: Linux distribution with .NET 8.0 support (Ubuntu 20.04+, Debian 11+, etc.) or ChromeOS with Penguin container
- **Audio Subsystem**: One of PulseAudio, JACK, or ALSA
- **Runtime**: .NET 8.0 Runtime
- **Memory**: 2 GB RAM minimum, 4 GB recommended
- **Storage**: 100 MB for application, plus space for music library

For detailed implementation status and developer notes, see [Linux Audio Implementation](./docs/LINUX_AUDIO_IMPLEMENTATION.md).

#### macOS Requirements
- **Operating System**: macOS 10.15+
- **Runtime**: .NET 8.0 Runtime
- **Memory**: 2 GB RAM minimum, 4 GB recommended
- **Storage**: 100 MB for application, plus space for music library
- **Note**: macOS audio support is planned using CoreAudio framework.

For more platform details, see [Cross-Platform Compliance](./docs/CROSS_PLATFORM_COMPLIANCE.md).

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

1. **Clone the repository**:
   ```powershell
   git clone https://github.com/mcdorgle/openbroadcaster.git
   cd openbroadcaster
   ```

2. **Restore dependencies**:
   ```powershell
   dotnet restore
   ```

3. **Build the solution**:
   ```powershell
   dotnet build -c Release
   ```

4. **Run the application**:
   ```powershell
   dotnet run --project OpenBroadcaster.csproj
   ```

   Or navigate to `bin/Release/net8.0-windows/` and run `OpenBroadcaster.exe`

### Running Tests

```powershell
dotnet test OpenBroadcaster.Tests/
```

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

## Project Structure

```
OpenBroadcaster/
├── Core/
│   ├── Audio/           # Audio playback, routing, VU meters
│   ├── Automation/      # AutoDJ, rotation engine, clockwheel
│   ├── Diagnostics/     # Logging infrastructure
│   ├── Messaging/       # Event bus and internal messaging
│   ├── Models/          # Data models (Track, QueueItem, etc.)
│   ├── Overlay/         # OBS overlay server and snapshots
│   ├── Requests/        # Request policy evaluation
│   ├── Services/        # Core services (Queue, Transport, etc.)
│   └── Streaming/       # Encoder manager, Icecast/Shoutcast
├── Views/               # XAML views and dialogs
├── ViewModels/          # MVVM view models
├── Converters/          # WPF value converters
├── Behaviors/           # WPF behaviors
├── Themes/              # Application themes and styles
├── Overlay/             # HTML/CSS/JS overlay assets
├── Properties/          # Assembly info
└── OpenBroadcaster.Tests/  # Unit tests
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| NAudio | 2.2.1 | Audio playback and routing |
| NAudio.Lame | 2.0.0 | MP3 encoding for streaming |
| TagLibSharp | 2.3.0 | Audio metadata extraction |
| Serilog | 3.1.1 | Structured logging |
| Serilog.Sinks.File | 5.0.0 | File-based log output |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | Logging abstractions |

## Configuration Files

- **Settings**: `%AppData%/OpenBroadcaster/settings.json`
- **Logs**: `%AppData%/OpenBroadcaster/logs/`
- **Library Database**: `%AppData%/OpenBroadcaster/library.json`

## Building & Running

### Windows

```bash
dotnet build -c Release
dotnet run
```

### Linux (New - Experimental)

**⚠️ IMPORTANT: Linux support is experimental and not fully tested.**
If you encounter any issues, please [report them on GitHub](https://github.com/rundleshow/OpenBroadcaster/issues).

#### Prerequisites

- **.NET 8.0 Runtime** or SDK
- **FFmpeg** - for audio codec support
- **PulseAudio** or **ALSA** - audio system
- **Audio Tools**: `ffplay`, `paplay`, `pactl`

**Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install -y \
  dotnet-sdk-8.0 \
  ffmpeg \
  pulseaudio-utils \
  alsa-utils
```

**Fedora:**
```bash
sudo dnf install -y \
  dotnet-sdk-8.0 \
  ffmpeg \
  pulseaudio-utils \
  alsa-utils
```

#### Build & Run

```bash
# Build
dotnet build -c Release

# Run
dotnet run --configuration Release
```

#### Application Data

- **Settings**: `~/.config/OpenBroadcaster/`
- **Logs**: `~/.config/OpenBroadcaster/logs/`
- **Library Database**: `~/.config/OpenBroadcaster/library.json`

#### Known Limitations

- Desktop audio capture not implemented (use microphone input or line-in)
- WASAPI loopback not available (use PulseAudio or ALSA)
- Tested on Ubuntu 22.04 LTS and Debian 12

#### Linux Documentation

For detailed information about Linux implementation and architecture, see:

- [LINUX_COMPATIBILITY_DOCUMENTATION_INDEX.md](LINUX_COMPATIBILITY_DOCUMENTATION_INDEX.md) - Start here
- [LINUX_AUDIT_SUMMARY.md](LINUX_AUDIT_SUMMARY.md) - Executive summary
- [LINUX_COMPATIBILITY_CHECKLIST.md](LINUX_COMPATIBILITY_CHECKLIST.md) - Deployment guide
- [LINUX_COMPATIBILITY_ARCHITECTURE.md](LINUX_COMPATIBILITY_ARCHITECTURE.md) - Technical details
- [QUICK_REFERENCE_LINUX_COMPATIBILITY.md](QUICK_REFERENCE_LINUX_COMPATIBILITY.md) - Code patterns

For AutoDJ, Rotation, and Scheduler verification:
- [AUTODJ_ROTATION_SCHEDULER_VERIFICATION.md](AUTODJ_ROTATION_SCHEDULER_VERIFICATION.md) - System verification

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Testing

**Important**: Linux support is experimental. When testing on Linux, please:
- Document your Linux distribution and version
- Note any audio-related issues
- Report device detection problems
- Submit any streaming or encoder issues

**Open an Issue** if you encounter problems - your feedback helps improve Linux support!

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) for excellent .NET audio libraries
- [TagLib#](https://github.com/mono/taglib-sharp) for metadata reading
- [Serilog](https://serilog.net/) for structured logging
- The open-source radio automation community

---

**OpenBroadcaster** - Professional Internet Radio Automation for Everyone
