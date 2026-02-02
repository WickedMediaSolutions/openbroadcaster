# OpenBroadcaster Linux Compatibility - Developer Checklist

## Pre-Deployment Verification

### System Requirements
- [ ] Linux kernel 4.4+ (ALSA support)
- [ ] .NET 8.0 Runtime installed
- [ ] ffmpeg available in PATH: `which ffmpeg`
- [ ] ffplay available in PATH: `which ffplay`
- [ ] paplay available in PATH: `which paplay`
- [ ] pactl available in PATH: `which pactl`

### Audio Device Verification
```bash
# Check ALSA devices
aplay -l
arecord -l

# Check PulseAudio
pactl list sources
pactl list sinks

# Verify ALSA capture device (ChromeOS/Crostini)
ls -la /dev/snd/pcmC0D0c
```

### Runtime Checks
- [ ] Application starts without errors
- [ ] Settings file created in ~/.config/OpenBroadcaster/
- [ ] Logs written to ~/.config/OpenBroadcaster/logs/
- [ ] Audio input device list populates
- [ ] Audio output device list populates
- [ ] VU meter responds to audio input
- [ ] File playback works (any format supported by ffmpeg)

---

## Code Review Checklist

When adding new features, verify:

### Platform Detection
- [ ] All Windows-specific code wrapped in `#if NET8_0_WINDOWS`
- [ ] All Linux-specific code guarded with `OperatingSystem.IsLinux()`
- [ ] Fallback code provided for unknown platforms
- [ ] No hardcoded assumptions about OS

### File I/O
- [ ] All paths use `Path.Combine()`
- [ ] Settings use `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
- [ ] No hardcoded C:\, /home/, or /tmp/ paths
- [ ] Directory creation uses `Directory.CreateDirectory()`
- [ ] File operations use `File.{Read,Write}AllText()` or `FileStream`

### External Processes
- [ ] `UseShellExecute = false`
- [ ] `CreateNoWindow = true`
- [ ] Arguments passed via `ArgumentList` (not string concatenation)
- [ ] Process streams properly disposed
- [ ] Process lifetime managed (Kill on timeout/cancellation)

### Networking & Sockets
- [ ] Using `TcpClient` (not WinSock)
- [ ] Using `SslStream` (not SSPI)
- [ ] No Windows registry or named pipes
- [ ] Protocol handling platform-agnostic

### Audio
- [ ] Audio I/O uses factory pattern for platform selection
- [ ] Capture/playback implementations exist for both Windows and Linux
- [ ] No hardcoded device names or paths
- [ ] Device enumeration works on target platform

### Encoding & Serialization
- [ ] String encoding uses `Encoding.UTF8`
- [ ] Protocol headers use `Encoding.ASCII`
- [ ] Number parsing uses `CultureInfo.InvariantCulture`
- [ ] JSON serialization cross-platform compatible

### Threading
- [ ] Using `async/await` or `Task` (not `BeginInvoke`)
- [ ] Using `CancellationToken` for cancellation
- [ ] No `SynchronizationContext.Post()` to specific thread

---

## Testing Checklist

### Unit Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj
```

- [ ] All platform-conditional code compiles on both Windows and Linux
- [ ] Audio tests pass (or mocked appropriately)
- [ ] File I/O tests use cross-platform paths
- [ ] Serialization tests produce consistent output

### Integration Tests (Manual)
```bash
# Build for Linux
dotnet build -c Release

# Run application
./bin/Release/net8.0/linux-x64/openbroadcaster

# Or from any platform
dotnet run --configuration Release
```

- [ ] Application starts without platform-specific errors
- [ ] Settings file created with correct permissions
- [ ] Audio devices enumerated correctly
- [ ] Can load and play audio files
- [ ] Can stream to Icecast/Shoutcast servers
- [ ] Twitch integration connects successfully

### Platform-Specific Tests

#### Linux
```bash
# Test audio input
arecord -f cd | aplay

# Test PulseAudio
pactl info

# Test ffmpeg capture
ffmpeg -f alsa -i hw:0,0 -f s16le -ar 44100 -ac 2 pipe:1 | head -c 44100 | ffplay -f s16le -ar 44100 -ac 2 -i pipe:0
```

#### Windows
- [ ] WaveIn device detection works
- [ ] WASAPI loopback captures desktop audio
- [ ] LAME MP3 encoding produces valid files

---

## Linux Distribution Testing

Test on:
- [ ] Ubuntu 22.04+ (common for streaming)
- [ ] Debian 12+ (stable baseline)
- [ ] Fedora 38+ (Red Hat ecosystem)
- [ ] Chrome OS Crostini (primary use case)
- [ ] Raspberry Pi OS (ARM platform)

### Each Distribution Should Verify:
```bash
# Audio stack identification
aplay --version           # ALSA
pactl info               # PulseAudio
pw-cli info core.0       # PipeWire (optional)
ffmpeg -version          # FFmpeg

# Application startup
dotnet --version         # .NET 8.0+
openbroadcaster          # Launches without errors
```

---

## Known Platform Limitations

### Windows
- ✅ Full NAudio support (WaveOut, WASAPI)
- ✅ LAME MP3 encoding
- ✅ Desktop audio capture

### Linux
- ✅ PulseAudio/ALSA microphone capture
- ✅ FFmpeg-based audio playback & encoding
- ⚠️  Desktop audio capture not implemented (use line-in or mic)
- ⚠️  No WASAPI equivalent (use PulseAudio loopback if needed)

### macOS
- ⚠️  Not tested (should work with minor modifications)
- Would need: AudioToolbox audio, CoreAudio for device enumeration
- Build would require `--os osx` RID

---

## Troubleshooting Guide

### "ffmpeg not found"
```bash
sudo apt-get install ffmpeg          # Ubuntu/Debian
sudo dnf install ffmpeg              # Fedora
```

### "pactl command not found"
```bash
sudo apt-get install pulseaudio-utils  # Ubuntu/Debian
sudo dnf install pulseaudio-utils      # Fedora
```

### "No audio devices detected"
```bash
# Check if PulseAudio is running
pulseaudio --check && echo "Running" || echo "Not running"

# Start PulseAudio
pulseaudio -D

# Check ALSA devices
aplay -l
arecord -l

# Verify permission to /dev/snd/
ls -la /dev/snd/
```

### "Settings file not writable"
```bash
mkdir -p ~/.config/OpenBroadcaster
chmod 755 ~/.config/OpenBroadcaster
```

### "FFmpeg capture failing"
```bash
# Test direct ALSA capture
ffmpeg -f alsa -i hw:0,0 -f s16le -ar 44100 -ac 2 -t 5 test.pcm

# Test PulseAudio capture
ffmpeg -f pulse -i default -f s16le -ar 44100 -ac 2 -t 5 test.pcm
```

---

## Performance Considerations

### Recommended Linux Setup for Broadcasting
```
CPU:         4+ cores (2 GHz+)
RAM:         2+ GB
Storage:     500 MB for application
Network:     Gigabit Ethernet or better

Audio Setup:
- PulseAudio daemon running
- ALSA configured for your audio devices
- FFmpeg compiled with libmp3lame support
```

### Profiling on Linux
```bash
# Monitor CPU usage
top -p $(pgrep openbroadcaster)

# Monitor memory
ps aux | grep openbroadcaster

# Monitor audio devices
watch -n 1 'pactl list sources | grep -A 5 "State:"'

# Check file descriptors
lsof -p $(pgrep openbroadcaster) | grep audio
```

---

## Continuous Integration Setup

### GitHub Actions Example
```yaml
name: Linux Build & Test
on: [push, pull_request]
jobs:
  linux:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0'
      - name: Install Linux dependencies
        run: |
          sudo apt-get update
          sudo apt-get install -y ffmpeg pulseaudio-utils
      - name: Build
        run: dotnet build -c Release
      - name: Test
        run: dotnet test
      - name: Verify platform conditionals
        run: grep -r "#if NET8_0_WINDOWS" Core/ | wc -l
```

---

## Release Checklist

Before releasing:

- [ ] All platform checks pass
- [ ] Unit tests 100% pass
- [ ] Manual testing on Linux (Ubuntu/Debian)
- [ ] Manual testing on Windows (if applicable)
- [ ] Verify settings files created with proper permissions
- [ ] Check log file generation and rotation
- [ ] Verify audio device detection works
- [ ] Test streaming to Icecast server
- [ ] Test Twitch integration
- [ ] Check crash logs generate properly
- [ ] Verify installer includes all required dependencies

---

## Documentation for Users

Create end-user documentation covering:

1. **Installation**
   - Prerequisites (ffmpeg, PulseAudio)
   - Docker image (recommended)
   - Package managers (snap, appimage, deb)

2. **Audio Setup**
   - How to test audio devices
   - PulseAudio configuration
   - ALSA fallback setup

3. **Troubleshooting**
   - Common errors and solutions
   - Logs location and interpretation
   - Support contact information

---

**Last Updated:** 2024  
**Compatibility:** Linux, Windows, cross-platform .NET 8.0
