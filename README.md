# OpenBroadcaster

**Professional Internet Radio Automation Software**

Version 4.5.0 | Cross-Platform | Open Source

---

## Overview

OpenBroadcaster is a professional-grade radio automation system designed for internet broadcasters, podcasters, and streaming enthusiasts. Built with modern .NET 8 and Avalonia UI, it delivers powerful features in a sleek, customizable interface.

## Features

### 🎵 Audio Management

- **Dual Deck System**: Seamless playback with Deck A and Deck B
- **Live Music Library**: Import, organize, and search your entire music collection
- **Album Art Display**: Beautiful visual representation of your tracks
- **Category Management**: Organize tracks by genre, mood, or custom categories
- **Dynamic Queue**: Build and manage your broadcast queue with drag-and-drop

### 🎛️ Control Rack

- **Professional VU Meters**: Real-time audio level monitoring for all channels
- **Rotary Knob Controls**: Precision volume control with hardware-style interface
- **Individual Channel Mixing**:
  - Master Output
  - Microphone Input
  - Cart Wall
  - Encoder Output
- **Live ON AIR Indicator**: Classic broadcast-style visual indicator

### 🎚️ Cart Wall

- **12 Programmable Cart Buttons**: Quick access to jingles, sound effects, and station IDs
- **Hotkey Support**: Keyboard shortcuts for instant playback
- **Visual Playback Feedback**: 
  - Color-coded playing state (matches theme)
  - 5-second countdown flash warning
  - Remaining time display
- **Loop Mode**: Continuous playback for beds and ambience

### 🎨 Theme System

Four professionally designed themes:
- **Default**: Modern dark blue
- **RetroRadio**: Classic green broadcast console
- **Bloody**: Deep red professional
- **Neon Frizzle**: Bright cyan modern

All themes feature:
- Pure black backgrounds for reduced eye strain
- Consistent black knobs with white indicators
- Color-coordinated panels, borders, and accents

### 🤖 Automation

- **Auto DJ**: Intelligent playlist management
- **Rotation Scheduler**: Genre-based rotation system
- **Time-of-Day Programming**: Schedule different content for different times
- **Overlay System**: Create audio beds and station imaging

### 💬 Twitch Integration

- **Live Chat**: Real-time Twitch chat integration
- **Customizable Font Size**: Adjust chat display to your preference
- **OAuth Authentication**: Secure connection to your Twitch channel

### 📡 Broadcasting

- **Built-in Encoder**: Stream directly to Icecast/Shoutcast servers
- **Multiple Bitrate Support**: Configure quality for your audience
- **Metadata Updates**: Automatic now-playing information
- **Stream Monitoring**: Real-time connection status and stats

### 🔧 Professional Features

- **Relay Service**: Remote control and status monitoring
- **Direct Server**: API for external integrations
- **WordPress Plugin Integration**: Display now-playing on your website
- **Overlay Services**: Advanced audio processing and imaging

## System Requirements

### Windows
- Windows 10 or later (64-bit)
- .NET 8 Runtime (included in installer)
- 4GB RAM minimum (8GB recommended)
- 500MB disk space for application
- Additional space for music library

### Linux (Ubuntu/Debian)
- Ubuntu 20.04 LTS or later
- .NET 8 Runtime
- ALSA or PulseAudio
- 4GB RAM minimum
- X11 or Wayland display server

## Installation

### Windows

1. Download `OpenBroadcaster-4.5.0-Setup.exe` from [Releases](https://github.com/WickedMediaSolutions/openbroadcaster/releases)
2. Run the installer
3. Follow the setup wizard
4. Launch OpenBroadcaster from the Start Menu

### Linux

```bash
# Install .NET 8 Runtime
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Clone the repository
git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
cd openbroadcaster

# Build and run
dotnet build
dotnet run --project OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj
```

## Quick Start

1. **Import Music**: Click "Import Folder" to add your music library
2. **Configure Categories**: Set up rotation categories for your format
3. **Load Cart Wall**: Assign jingles and sound effects to cart buttons
4. **Set Up Encoder**: Configure your streaming server details
5. **Enable Auto DJ**: Let OpenBroadcaster manage your playlist
6. **Go Live**: Hit the "Talk" button when you're ready to broadcast

## Configuration

### Audio Settings
- Configure input/output devices
- Set buffer sizes for latency
- Adjust crossfade timing
- Enable/disable ducking

### Encoder Settings
- Server URL and port
- Mount point
- Bitrate selection
- Metadata format

### Twitch Settings
- OAuth token configuration
- Channel name
- Chat font size
- Auto-connect on startup

## Keyboard Shortcuts

- **Cart Buttons**: Configurable hotkeys (F1-F12)
- **Space**: Quick mic toggle
- **Deck Controls**: Customizable keybindings

## Support

- **Creator**: Josh Rundle
- **Twitch**: [twitch.tv/bluntforcejosh](https://twitch.tv/bluntforcejosh)
- **GitHub Issues**: [Report Bugs & Request Features](https://github.com/WickedMediaSolutions/openbroadcaster/issues)
- **Discussions**: [Community Forum](https://github.com/WickedMediaSolutions/openbroadcaster/discussions)

## Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
cd openbroadcaster

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj
```

### Project Structure

- `OpenBroadcaster.Avalonia/` - Main UI application (Avalonia)
- `Core/` - Core business logic and services
- `OpenBroadcaster.RelayService/` - Remote control service
- `wordpress-plugin-v2/` - WordPress integration plugin
- `installer/` - Windows installer scripts

### Technologies

- **.NET 8**: Modern cross-platform framework
- **Avalonia UI**: Cross-platform XAML-based UI framework
- **NAudio**: Audio playback and processing (Windows)
- **BASS.NET**: Professional audio library
- **Newtonsoft.Json**: Configuration and data management

## License

**Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**

You are free to:
- ✅ Share: Copy and redistribute the material
- ✅ Adapt: Remix, transform, and build upon the material

Under the following terms:
- 📝 **Attribution**: You must give appropriate credit
- 🚫 **NonCommercial**: You may not use the material for commercial purposes

See [LICENSE](LICENSE) for full details.

## Acknowledgments

- NAudio and BASS.NET for audio capabilities
- Avalonia community for the amazing UI framework
- All contributors and testers

## Changelog

### Version 4.5.0 (March 2026)
- ✨ Enhanced theme system with 4 professional color schemes
- 🎨 Improved cart wall with theme-synchronized colors
- 🎛️ Fixed rotary knobs with consistent black/white styling
- 📺 Custom ON AIR indicator with red illumination effect
- 🎯 Console-style control rack buttons
- 🔧 Theme selector in title bar for easy access
- 📊 Performance improvements and bug fixes

### Version 4.4.0 (February 2026)
- Cross-platform support (Windows/Linux)
- Avalonia UI migration
- Enhanced audio abstraction layer
- Improved stability

### Version 4.0-4.3
- Legacy WPF versions
- Core feature development

---

**Made with ❤️ for Radio Broadcasters Everywhere**

[Star this repo](https://github.com/WickedMediaSolutions/openbroadcaster) if you find it useful!
