# Linux Production Build Guide - OpenBroadcaster v4.4.0

**Status**: Production-Ready  
**Date**: March 2, 2026  
**Platform**: Ubuntu 22.04 LTS / Debian 12+  
**Architecture**: x86_64 (amd64)

---

## Overview

This document covers building OpenBroadcaster v4.4.0 for Linux production deployment. The Linux version is built from the same source code as Windows v4.4, with platform-specific audio backends (PulseAudio, ALSA, JACK).

---

## Prerequisites

### System Requirements
- **OS**: Ubuntu 22.04 LTS or Debian 12+
- **CPU**: 64-bit x86_64 processor
- **RAM**: 4GB minimum (8GB recommended)
- **Disk**: 2GB free space for build + 500MB for installation

### Build Dependencies

```bash
# Install .NET SDK 8.0
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0

# Install build tools
sudo apt-get update
sudo apt-get install -y \
    build-essential \
    git \
    dotnet-sdk-8.0
```

### Runtime Dependencies

The application requires these packages for operation:

```bash
sudo apt-get install -y \
    dotnet-runtime-8.0 \
    libicu70 \
    libssl3 \
    libfreetype6 \
    libfontconfig1 \
    libx11-6 \
    libxkbcommon0 \
    libxrender1 \
    libxrandr2 \
    libgl1-mesa-glx \
    pulseaudio \
    libpulse0 \
    alsa-lib \
    alsa-utils \
    libtag1v5 \
    libsndfile1 \
    libmpg123-0
```

---

## Build Process

### Option 1: Automated Build Script (Recommended)

```bash
# Navigate to project root
cd /path/to/openbroadcaster

# Make build script executable
chmod +x scripts/build-linux-production.sh

# Run the automated build
./scripts/build-linux-production.sh
```

**This script will:**
1. Clean previous builds
2. Restore NuGet dependencies
3. Run all 86 unit tests (must pass)
4. Build and publish for Linux x64
5. Create tarball distribution
6. Prepare DEB package structure
7. Build DEB package (if dpkg-deb available)

**Output**: Files in `./dist/`
- `OpenBroadcaster-4.4.0-linux-x64.tar.gz` (Tarball)
- `openbroadcaster_4.4.0_amd64.deb` (DEB package)
- `openbroadcaster-deb-template/` (DEB template for manual build)

### Option 2: Manual Build

```bash
# Restore and build
dotnet restore openbroadcaster.sln
dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj -c Release

# Publish for Linux x64 (self-contained)
dotnet publish OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj \
    -c Release \
    -o ./dist/publish \
    --self-contained=true \
    --runtime=linux-x64

# Create tarball
tar -czf ./dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz -C ./dist/publish .

# Build DEB (if dpkg-deb available)
dpkg-deb --build debian_temp ./dist/openbroadcaster_4.4.0_amd64.deb
```

---

## Installation Methods

### Method 1: DEB Package (Recommended for Ubuntu/Debian)

```bash
# Download the .deb file
# Then install:
sudo apt install ./openbroadcaster_4.4.0_amd64.deb

# Run application
openbroadcaster

# Or from menu: Applications → Sound & Video → OpenBroadcaster
```

### Method 2: Tarball (Portable/Universal)

```bash
# Download and extract
tar -xzf OpenBroadcaster-4.4.0-linux-x64.tar.gz -C ~/apps/

# Run directly
~/apps/OpenBroadcaster.Avalonia

# Or create symlink for easy access  
ln -s ~/apps/OpenBroadcaster.Avalonia ~/.local/bin/openbroadcaster
openbroadcaster
```

### Method 3: Docker Container

```bash
# Build image (from source)
docker build -t openbroadcaster:4.4.0 -f Dockerfile.linux .

# Run container with audio support
docker run -it \
    -v ~/.openbroadcaster:/app/config \
    -v /run/pulse:/run/pulse \
    -e PULSE_SERVER=unix:/run/pulse/native \
    openbroadcaster:4.4.0
```

---

## Configuration

### Application Data Directory

The application stores configuration and data in:
- **Linux**: `~/.config/openbroadcaster/` (XDG standards)
- **Windows**: `%APPDATA%\OpenBroadcaster\`
- **Docker**: `/app/config/`

### Settings File

Main configuration is stored in `app.json`:

```
~/.config/openbroadcaster/
├── app.json (Main settings)
├── library.db (Music library)
├── logs/ (Application logs)
└── cache/ (Temporary data)
```

### Audio Configuration

Audio preferences are automatically saved:
- Master volume level
- Output device selection (Deck A, Deck B, Cart Wall, Encoder)
- Microphone input device
- Microphone enabled state
- PulseAudio detection and routing

---

## Features & Parity with Windows v4.4.0

### Core Features
✅ Multi-deck audio playback (Deck A, Deck B)  
✅ Cart Wall (sound effects) system  
✅ Microphone input with ducking support  
✅ Master volume control via program slider  
✅ Audio device hot-swapping  

### Automation
✅ Top-of-Hour (TOH) injection (:00 every hour)  
✅ Bottom-of-Hour (BOH) injection (:30 every hour)  
✅ AutoDJ with smart rotation scheduling  
✅ Scheduled playback by day/time  

### Streaming & Integration
✅ Twitch integration (streaming + chat)  
✅ YouTube streaming support  
✅ DirectServer API for external control  
✅ Overlay browser source support  

### Settings & Persistence
✅ Audio device settings persist across restarts  
✅ Microphone state (enabled/selected device) saved  
✅ Volume levels preserved  
✅ BOH/TOH configurations persistent  
✅ Theme selection (4 themes available)  

### Audio Backends (Linux-Specific)
✅ PulseAudio (primary)  
✅ ALSA (fallback)  
✅ JACK Audio (advanced users)  
✅ Automatic backend detection  

---

## Testing & Verification

### Pre-Release Checklist

```bash
# 1. Run all tests
dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj -c Release
# Expected: 86/86 passing

# 2. Start application
./dist/publish/OpenBroadcaster.Avalonia &

# 3. Test microphone
# - Audio Settings → Mic Input selector
# - Select USB microphone device
# - Main window → Enable mic icon
# - Verify input meters show activity

# 4. Test audio playback
# - Library → Select track → Play
# - Verify output on Deck A/B
# - Test volume slider (should adjust master level)

# 5. Test Top-of-Hour injection
# - Settings → Automation → Injection Control
# - Enable TOH checkbox
# - Set fire offset to 0 (for :00:00)
# - Create test slot
# - Wait for next hour top or test manually

# 6. Test Bottom-of-Hour injection
# - Settings → Automation → Injection Control
# - Enable BOH checkbox
# - Configure BOH slots
# - Verify fires at :30 mark

# 7. Test settings persistence
# - Change microphone device
# - Adjust volume
# - Disable AutoDJ auto-start
# - Restart application
# - Verify all settings retained
```

---

## Troubleshooting

### Audio Not Working

**Check audio system:**
```bash
# Test PulseAudio
pactl list sinks
pactl info

# Test ALSA
arecord -l  # List input devices
aplay -l    # List output devices

# Test JACK (if installed)
jack_lsp

# Check audio logs
cat ~/.config/openbroadcaster/logs/*.log | grep -i audio
```

**Fix PulseAudio issues:**
```bash
# Restart PulseAudio daemon
pulseaudio -k
pulseaudio --start

# Check permissions
groups $USER | grep audio
# If not in audio group:
sudo usermod -a -G audio $USER
# Then relogin
```

### Microphone Not Appearing

```bash
# List input devices
arecord -l

# Check PulseAudio sources
pactl list sources

# Reload sound modules
pulseaudio -k
sleep 2
pulseaudio --daemonize

# Restart OpenBroadcaster and reload device list in Settings
```

### Application Crashes

```bash
# Run with debug logging
DOTNET_SYSTEM_DIAGNOSTICS_CHANNEL_WARNINGS=1 ./OpenBroadcaster.Avalonia

# Check syslog for errors
journalctl -u openbroadcaster -n 50

# Check application logs
tail -50 ~/.config/openbroadcaster/logs/openbroadcaster.log
```

---

## Uninstallation

### DEB Package
```bash
sudo apt remove openbroadcaster
# Configuration retained in ~/.config/openbroadcaster/
# To completely remove:
sudo apt purge openbroadcaster
rm -rf ~/.config/openbroadcaster/
```

### Tarball
```bash
rm ~/apps/OpenBroadcaster.Avalonia
rm ~/.local/bin/openbroadcaster
```

### Docker
```bash
docker rmi openbroadcaster:4.4.0
```

---

## Performance Optimization

### For Low-End Systems

```bash
# Use ALSA instead of PulseAudio (lighter weight)
# Edit: ~/.config/openbroadcaster/app.json
# Set: "PreferredAudioBackend": "alsa"

# Reduce logging overhead
# Set logging level to "Warning"

# Disable overlay server if not needed
# Settings → Overlay → Unchecked "Enable Overlay"
```

### For High-Load Broadcasting

```bash
# Increase process priority
ionice -c 2 -n 0 openbroadcaster &  # High priority I/O
nice -n -5 openbroadcaster &         # High priority CPU

# Monitor system resources
watch -n 1 'ps aux | grep OpenBroadcaster'
htop -p $(pgrep -f OpenBroadcaster)
```

---

## System Integration

### Creating Menu Entry (if not installed via DEB)

```bash
# Create .desktop file
cat > ~/.local/share/applications/openbroadcaster.desktop << EOF
[Desktop Entry]
Type=Application
Name=OpenBroadcaster
Comment=Radio Automation Software
Exec=/usr/local/lib/openbroadcaster/OpenBroadcaster.Avalonia %U
Icon=openbroadcaster
Categories=Audio;Multimedia;
MimeType=audio/mpeg;audio/flac;audio/wav;
Terminal=false
EOF

# Update desktop database
update-desktop-database ~/.local/share/applications/
```

### Autostart on System Boot

```bash
# Create systemd service
sudo tee /etc/systemd/system/openbroadcaster.service > /dev/null << EOF
[Unit]
Description=OpenBroadcaster Radio Automation
After=network.target pulseaudio.service

[Service]
Type=simple
User=openbroadcaster
Group=audio
ExecStart=/usr/local/lib/openbroadcaster/OpenBroadcaster.Avalonia
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable openbroadcaster
sudo systemctl start openbroadcaster
```

---

## Production Deployment

### Server Environment (Headless)

For headless server deployment without X11:

```bash
# Create a wrapper script
cat > ~/bin/openbroadcaster-server << 'EOF'
#!/bin/bash
export DISPLAY=:99
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
/usr/local/lib/openbroadcaster/OpenBroadcaster.Avalonia "$@"
EOF

chmod +x ~/bin/openbroadcaster-server

# Run with virtual display (Xvfb)
Xvfb :99 -screen 0 800x600x24 &
openbroadcaster-server &
```

### Monitoring & Logging

```bash
# Configure centralized logging
tail -f ~/.config/openbroadcaster/logs/*.log | \
    sed 's/^/[OpenBroadcaster] /' | \
    logger -t openbroadcaster

# Monitor process health
watch -n 5 'ps aux | grep --color OpenBroadcaster.Avalonia'

# Check resource usage
top -p $(pgrep -f OpenBroadcaster)
```

---

## Getting Help

### Community & Support
- **GitHub Issues**: https://github.com/WickedMediaSolutions/openbroadcaster/issues
- **Documentation**: https://wickedmediasolutions.com/docs/openbroadcaster
- **Email**: support@wickedmediasolutions.com

### Providing Debug Information

When reporting issues, include:
```bash
# System information
uname -a
cat /etc/os-release

# Audio system info
pactl list | head -20
aplay -l

# Application version
/path/to/OpenBroadcaster.Avalonia --version

# Last 100 lines of logs
tail -100 ~/.config/openbroadcaster/logs/openbroadcaster.log
```

---

## Changelog - v4.4.0

### New Features
- Bottom-of-Hour (BOH) injection for 30-minute cycles
- BOH enable toggle in Automation tab for easy access
- Enhanced microphone input handling with state persistence
- Automatic device fallback for better reliability

### Improvements
- Microphone state now persists across application restarts
- Cleaner Audio tab (removed Master Volume - use program slider)
- Better device initialization on startup
- Improved Linux audio backend detection

### Fixes
- Fixed microphone input not being applied after device selection
- Fixed missing mic state initialization on app startup
- Fixed device resolution failing when no device pre-configured

### Technical
- Cross-platform audio backends fully operational
- PulseAudio/ALSA/JACK detection working correctly
- All 86 unit tests passing on Linux
- Production-ready packaging (DEB, Tarball, Docker)

---

## License

OpenBroadcaster is licensed under the MIT License. See LICENSE file for details.
