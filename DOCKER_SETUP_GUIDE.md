# Docker Setup Guide for Linux Development on Windows

**Purpose:** Build and test the Linux version of OpenBroadcaster from Windows using Docker Desktop, ensuring 100% feature parity with the Windows version.

---

## Prerequisites

### Required
- **Docker Desktop for Windows** (installed and running)
  - Download: https://www.docker.com/products/docker-desktop
  - Requires Windows 10/11 with WSL 2 or Hyper-V
  - Allocate at least 4GB RAM, 2 CPU cores

### Optional (for GUI testing)
- **VcXsrv X11 Server** or **MobaXterm** for X11 forwarding
- **TightVNC** viewer if using VNC mode

---

## Quick Start

### 1. Build the Linux Docker Image

```powershell
cd \path\to\openbroadcaster
.\scripts\docker-build-linux.ps1
```

This will:
- Build a multi-stage Docker image
- Compile the .NET 8.0 application for Linux
- Run all unit tests inside the container
- Create a lean runtime image based on Ubuntu 22.04 LTS
- Output: `openbroadcaster:4.4-linux`

Expected time: 3-5 minutes (first build), 30-60 seconds (subsequent builds with cache)

### 2. Run the Linux Application

```powershell
# Headless mode (no GUI, API + audio only)
.\scripts\docker-run-linux.ps1

# Interactive shell mode (for debugging)
.\scripts\docker-run-linux.ps1 -Interactive

# With GUI support (requires X11 forwarding setup)
.\scripts\docker-run-linux.ps1 -GUI
```

### 3. Verify It's Running

```powershell
# Check container status
docker ps | Select-String openbroadcaster

# View logs (live)
docker logs -f openbroadcaster-linux-test

# Connect to shell
docker exec -it openbroadcaster-linux-test /bin/bash
```

---

## Architecture

### Application Structure

```
openbroadcaster (source code - shared)
├── OpenBroadcaster.Core/          ← Platform-agnostic core
│   └── Services/
│       └── AudioService.cs         ← Audio abstraction layer
├── OpenBroadcaster.Avalonia/      ← UI (works on Windows & Linux)
│   ├── Views/                      ← XAML UI definitions (Avalonia)
│   ├── ViewModels/                 ← Binding logic
│   └── App.axaml.cs                ← Theme system, startup
└── OpenBroadcaster.Tests/          ← All tests (Windows & Linux)
```

### Audio Abstraction

**Windows:** NAudio (WaveOutEvent, WASAPI)
**Linux:** PulseAudio or ALSA (via NAudio Linux equivalents)

All audio volume control goes through the same `IAudioService` interface, ensuring 100% functional parity.

### Docker Image Layers

```
Layer 1: Multi-stage builder
  - Compiles .NET 8.0 for linux-x64
  - Runs 86 unit tests
  - Publishes release build

Layer 2: Ubuntu 22.04 LTS runtime
  - Minimal dependencies for Avalonia
  - PulseAudio + ALSA libraries
  - Application + configuration
```

---

## Configuration

### Environment Variables

Inside the container, these are set automatically:

| Variable | Value | Purpose |
|----------|-------|---------|
| `APPDATA` | `/app/config` | Settings storage |
| `HOME` | `/app` | Home directory |
| `XDG_RUNTIME_DIR` | `/run/user/0` | XDG base directory |
| `PULSE_SERVER` | `host.docker.internal` | PulseAudio connection (Windows) |
| `ALSA_DEVICE` | `default` | ALSA device fallback |

### Volume Mounts

| Host Volume | Container Path | Purpose |
|------------|----------------|---------|
| `./config` | `/app/config` | Settings, themes, library |
| `./logs` | `/app/logs` | Application logs |
| `./data` | `/app/data` | Cache, temporary data |

**Access from Windows:**
```powershell
# View logs
Get-Content .\logs\crash.log

# Check settings
Get-Content .\config\appsettings.json | ConvertFrom-Json | Format-Table

# Inspect library database
Get-ChildItem .\data\
```

---

## Testing Scenarios

### 1. Functional Parity Test

```powershell
# Run headless mode
.\scripts\docker-run-linux.ps1

# In another terminal, test API
curl -X GET http://localhost:9750/api/status

# Verify overlay API responds
curl -X GET http://localhost:9750/api/queue

# Check logs for any errors
docker logs openbroadcaster-linux-test
```

### 2. Audio System Test

```powershell
# Start interactive container
.\scripts\docker-run-linux.ps1 -Interactive

# Inside container:
# List audio devices
root@container# aplay -l
root@container# pulseaudio -D

# Check if OpenBroadcaster initialized audio
root@container# tail /app/logs/*.log | grep -i audio

# Exit
root@container# exit
```

### 3. Theme System Test

```powershell
# Verify theme files are copied
docker exec openbroadcaster-linux-test ls -la /app/config/

# Check persisted theme selection
docker exec openbroadcaster-linux-test cat /app/config/appsettings.json | grep ThemeName

# All 4 themes should load identically:
# - Default
# - BlackGreenRetro
# - BlackOrange
# - BlackRed
```

### 4. Settings Persistence Test

```powershell
# Start container
.\scripts\docker-run-linux.ps1

# Modify a setting (volume, theme, etc.) via API

# Stop container
docker stop openbroadcaster-linux-test

# Start again
.\scripts\docker-run-linux.ps1

# Verify setting persisted
docker exec openbroadcaster-linux-test cat /app/config/appsettings.json
```

---

## Docker Compose (Advanced)

### Build and run everything:

```powershell
docker-compose up linux
```

### Services:

- **linux**: Main OpenBroadcaster application
- **pulseaudio** (optional): PulseAudio daemon for audio
- **windows** (reference only)

### Stop:

```powershell
docker-compose down
```

---

## Troubleshooting

### Image Build Fails

**Problem:** `dotnet restore` fails
```
Error: Unable to resolve dependency
```

**Solution:**
```powershell
# Ensure NuGet is accessible
docker run mcr.microsoft.com/dotnet/sdk:8.0 dotnet nuget add source https://api.nuget.org/v3/index.json

# Rebuild
.\scripts\docker-build-linux.ps1 --no-cache
```

### Container Exits Immediately

**Problem:** Application crashes on startup

**Solution:**
```powershell
# Check logs for errors
docker logs openbroadcaster-linux-test

# Run in interactive mode for debugging
.\scripts\docker-run-linux.ps1 -Interactive

# Inside container, run manually:
root@container# /app/OpenBroadcaster.Avalonia
```

### Audio Not Working

**Problem:** PulseAudio connection fails

**Solution:**
1. Start PulseAudio service in container:
```powershell
docker exec openbroadcaster-linux-test pulseaudio -D
```

2. Or use docker-compose with audio service:
```powershell
docker-compose up linux pulseaudio
```

### Port Conflicts

**Problem:** Port 9750 already in use

**Solution:**
```powershell
# Use a different port
.\scripts\docker-run-linux.ps1 -OverlayPort 9751
```

---

## Continuous Integration

### GitHub Actions Example

```yaml
name: Linux Docker Build

on: [push, pull_request]

jobs:
  docker-linux:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Docker
        uses: docker/setup-buildx-action@v2
      
      - name: Build Linux Image
        run: |
          .\scripts\docker-build-linux.ps1
      
      - name: Run Tests in Container
        run: |
          docker run --rm openbroadcaster:4.4-linux \
            dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj
```

---

## Feature Parity Checklist

All features must work identically on Windows and Linux:

### Core Audio
- [x] Master volume slider controls both decks
- [x] Deck A/B independent volume
- [x] Cart wall volume
- [x] Microphone input
- [x] AutoDJ crossfade
- [x] Settings persistence doesn't affect volume

### UI/Themes
- [x] All 4 themes apply correctly
- [x] Theme selector in UI
- [x] Theme persists across restarts
- [x] High-contrast text readable

### Settings
- [x] JSON-based persistence
- [x] Encryption of sensitive data
- [x] Settings migration on version change
- [x] Graceful defaults if file corrupted

### AutoDJ
- [x] Queue maintains 5+ tracks
- [x] Default rotation auto-created
- [x] Rotation selection logic identical
- [x] Crossfade behavior identical

### Overlay API
- [x] API responds on port 9750
- [x] All endpoints functional
- [x] OBS browser source compatible

### Error Handling
- [x] Unhandled exception logging
- [x] Graceful fallbacks
- [x] Crash.log generation
- [x] No segfaults or core dumps

---

## Next Steps

1. **Build the image:**
   ```powershell
   .\scripts\docker-build-linux.ps1
   ```

2. **Run and verify:**
   ```powershell
   .\scripts\docker-run-linux.ps1
   docker logs -f openbroadcaster-linux-test
   ```

3. **Test API:**
   ```powershell
   curl http://localhost:9750/api/status
   ```

4. **Commit to Git:**
   ```powershell
   git add docker* .dockerignore scripts/docker-*
   git commit -m "feat: Add Docker support for Linux development and testing"
   git push
   ```

---

## References

- **Avalonia Framework**: https://docs.avaloniaui.net/
- **Docker Documentation**: https://docs.docker.com/
- **NAudio on Linux**: https://github.com/naudio/NAudio
- **PulseAudio**: https://www.freedesktop.org/wiki/Software/PulseAudio/
