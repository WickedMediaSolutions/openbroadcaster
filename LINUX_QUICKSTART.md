# OpenBroadcaster Linux - Quick Start Guide

## Prerequisites

You need to install system audio libraries **before** running OpenBroadcaster. This is a one-time setup.

### Automated Setup (Recommended)

```bash
./setup-linux.sh
```

This automatically:
1. Detects your Linux distribution
2. Installs required audio libraries
3. Creates configuration directories
4. Verifies everything is installed

### Manual Setup (Ubuntu/Debian)

```bash
sudo apt-get update
sudo apt-get install -y libmp3lame0 libopenal1 libpulse0 libsndfile1
```

### Manual Setup (Fedora/RHEL/CentOS)

```bash
sudo dnf install -y lame-libs openal-soft-libs pulseaudio-libs libsndfile
```

### Manual Setup (Arch Linux)

```bash
sudo pacman -S --noconfirm libmp3lame openal libpulse libsndfile
```

## Running the App

### From Source

```bash
cd openbroadcaster
dotnet run
```

### From Installed Package

```bash
# Extract the archive
tar -xzf OpenBroadcaster-1.3.0-linux-x64.tar.gz
cd OpenBroadcaster

# Run the setup script to install dependencies
./setup-linux.sh

# Run directly
./OpenBroadcaster

# Or install system-wide
sudo ./install.sh
openbroadcaster
```

## Key Features

- ✅ **Cart Wall** with remaining time countdown
- ✅ **Encoder Support** (Icecast/Shoutcast streaming)
- ✅ **OBS Overlay** integration via WebSocket
- ✅ **AutoDJ** with smart rotation
- ✅ **Twitch** integration for chat and events
- ✅ **Full CRUD** for tracks and categories

## Encoder Setup

1. **Start Icecast server** (if not already running):
   ```bash
   sudo systemctl start icecast2
   # or: docker run -p 8000:8000 infiniteproject/icecast:latest
   ```

2. **Configure encoder in OpenBroadcaster:**
   - Settings → Encoders → Add Profile
   - Host: `localhost` (or your server IP)
   - Port: `8000`
   - Mount: `/main` (or your mount point)
   - Username: `source`
   - Password: (your Icecast source password)
   - Protocol: `Icecast`
   - Format: `MP3`
   - Bitrate: `128` kbps

3. **Verify connectivity:**
   ```bash
   ./encoder-test.sh localhost 8000 /main source hackme
   ```

4. **Check logs if issues occur:**
   ```bash
   tail -f ~/.local/share/OpenBroadcaster/logs/application.log
   ```

## Audio Configuration

Go to Settings → Audio to configure:
- **Master Volume** - Overall output level
- **Deck A/B Output** - Where deck audio goes
- **Cart Wall Output** - Where cart wall audio goes
- **Encoder Bus Capture** - Audio source for streaming

## Troubleshooting

### "libmp3lame not found" or similar

Re-run setup script:
```bash
./setup-linux.sh
```

Or manually install:
```bash
sudo apt-get install libmp3lame0
```

### Encoder won't connect

1. Verify Icecast is running:
   ```bash
   netstat -tulpn | grep 8000
   ```

2. Test connectivity:
   ```bash
   ./encoder-test.sh localhost 8000 /main
   ```

3. Check logs:
   ```bash
   cat ~/.local/share/OpenBroadcaster/logs/encoder/encoder-errors.log
   ```

### No audio output

1. Check audio device selection in Settings
2. Verify device is not muted: `pactl list sinks`
3. Check volume levels in Settings

## Documentation

- **LINUX_DEPENDENCIES.md** - Complete dependency guide
- **ENCODER_DIAGNOSTICS.md** - Encoder troubleshooting
- **ENCODER_DEBUG_SESSION.md** - Technical details

## Getting Help

1. Check the relevant documentation file
2. Review logs in `~/.local/share/OpenBroadcaster/logs/`
3. Run diagnostic tests: `./encoder-test.sh`
4. Include logs when reporting issues

## What to Expect

- First run creates config files in `~/.config/OpenBroadcaster/`
- Logs saved to `~/.local/share/OpenBroadcaster/logs/`
- Overlay accessible at `http://localhost:7777` (if enabled)
- Encoder streams to configured Icecast mount

Enjoy! 🎙️
