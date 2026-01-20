# OpenBroadcaster

A professional-grade internet radio automation and broadcasting application for Windows, built with WPF and .NET 8.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square&logo=windows)
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

- **Operating System**: Windows 10 or later (64-bit)
- **Runtime**: .NET 8.0 Desktop Runtime
- **Audio**: WASAPI-compatible audio devices
- **Memory**: 4 GB RAM minimum, 8 GB recommended
- **Storage**: 100 MB for application, plus space for music library

## Installation

### Prerequisites

1. Install the [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Using the Windows Installer

1. Download the latest `OpenBroadcaster-Setup.exe` from the GitHub Releases page
2. Run the installer and follow the prompts
3. Launch **OpenBroadcaster** from the Start Menu or desktop shortcut
4. Open **Settings → Audio** on first launch to verify devices

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

## Quick Start Guide

1. **Launch OpenBroadcaster** and configure your audio devices in **Settings → Audio**

2. **Import your music library**:
   - Go to **Library → Import Tracks** for individual files
   - Go to **Library → Scan Folders** for batch imports

3. **Organize with categories**:
   - Open **Library → Manage Categories**
   - Create categories (e.g., "Rock", "Pop", "Jingles")
   - Assign tracks to categories via right-click

4. **Set up automation** (optional):
   - Enable **AutoDJ** to automatically fill the queue
   - Configure rotation rules in Settings

5. **Configure streaming** (optional):
   - Add encoder profiles in **Settings → Encoders**
   - Enter your Shoutcast/Icecast server details
   - Enable encoder to start streaming

6. **Connect Twitch** (optional):
   - Enter credentials in **Settings → Twitch**
   - Enable chat bridge to accept song requests

7. **Start broadcasting**:
   - Drag tracks to the queue or enable AutoDJ
   - Hit play on Deck A
   - Monitor levels on VU meters

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

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) for excellent .NET audio libraries
- [TagLib#](https://github.com/mono/taglib-sharp) for metadata reading
- [Serilog](https://serilog.net/) for structured logging
- The open-source radio automation community

---

**OpenBroadcaster** - Professional Internet Radio Automation for Everyone
