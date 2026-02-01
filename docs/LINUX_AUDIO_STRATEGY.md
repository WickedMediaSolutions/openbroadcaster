# Linux Audio Implementation Strategy

**Date**: February 2026  
**Status**: 🚧 In Development - Abstraction Layer Complete, Engines in Development

---

## Executive Summary

OpenBroadcaster now ships a **Linux-ready audio path** built on:

1. **OpenAL (OpenTK)** for playback + capture
2. **FFmpeg CLI** for cross-format decode (MP3/WAV/FLAC/OGG/AAC/WMA)
3. **OpenAL device lists** with Pulse/ALSA fallback enumeration

Native PulseAudio/JACK/ALSA P/Invoke engines remain an optional future enhancement if deeper system integration is needed.

---

## Architecture Overview

### Abstraction Layer (✅ Complete)

The core abstraction consists of three interfaces that all backends must implement:

```csharp
// Playback/Output Interface
public interface IPlaybackEngine : IDisposable
{
    void Initialize(AudioFormat format);
    void Play(float[] samples, int offset, int count);
    void Pause();
    void Stop();
    void SetVolume(float volume);
    float GetVolume();
    PlaybackState GetState();
}

// Recording/Input Interface
public interface IRecordingEngine : IDisposable
{
    void Initialize(AudioFormat format);
    void StartRecording();
    void StopRecording();
    float[] ReadSamples(int count);
    RecordingState GetState();
}

// Device Enumeration Interface
public interface IAudioDeviceEnumerator
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    IReadOnlyList<AudioDeviceInfo> GetRecordingDevices();
}
```

### Platform Detection (✅ Complete)

`PlatformDetection.cs` provides runtime platform checks:

```csharp
public static class PlatformDetection
{
    public static bool IsWindows { get; }
    public static bool IsLinux { get; }
    public static bool IsMacOS { get; }
    public static bool SupportsWindowsAudio { get; }
    public static bool SupportsLinuxAudio { get; }
}
```

### Factory Pattern (✅ Complete)

`AudioEngineFactory` handles platform-specific instantiation:

```csharp
public static class AudioEngineFactory
{
    public static IPlaybackEngine CreatePlaybackEngine();
    public static IRecordingEngine CreateRecordingEngine();
    public static IAudioDeviceEnumerator CreateDeviceEnumerator();
}
```

---

## Implementation Status by Platform

### Windows (✅ Complete - Production Ready)

**Implementation**: 
- `WindowsPlaybackEngine` - Uses NAudio `WaveOutEvent`
- `WindowsRecordingEngine` - Uses NAudio `WaveInEvent`
- `WindowsAudioDeviceEnumerator` - Uses NAudio device enumeration

**Features**:
- Full audio playback and recording
- Device enumeration with friendly names
- Volume control
- All transport controls (play, pause, stop)

**Status**: Production-ready, actively used on Windows 10/11

---

### Linux (🚧 In Development)

#### Linux Audio Backend Detection (✅ Complete)

`LinuxAudioDetector` automatically probes for available audio systems:

```csharp
public class LinuxAudioDetector
{
    public static LinuxAudioBackend DetectAvailableBackend()
    {
        // 1. Check for PulseAudio (pactl --version)
        // 2. Check for JACK (jack_lsp availability)
        // 3. Check for ALSA (aplay --version)
        // 4. Return best available or throw if none found
    }
}
```

**Probe Order** (by priority):
1. **PulseAudio** - `pactl --version`
2. **JACK** - `jack_lsp` command availability
3. **ALSA** - `aplay --version` (always available as fallback)

#### Device Enumeration (✅ Complete for All Backends)

All three Linux backends have working device enumerators that parse command-line output:

| Backend | Device Enumerator | Parsing Method |
|---------|-------------------|-----------------|
| PulseAudio | `PulseAudioDeviceEnumerator` | `pactl list short sinks/sources` |
| JACK | `JackAudioDeviceEnumerator` | `jack_lsp -p` output parsing |
| ALSA | `AlsaDeviceEnumerator` | `aplay -l` / `arecord -l` output parsing |

**Status**: All enumerators working, return formatted `AudioDeviceInfo[]`

#### Playback Engines (🚧 Interface Contract Ready, Implementation Pending)

##### 1. PulseAudio Playback Engine

**Target Use Case**: Desktop Linux (Ubuntu, Debian, Fedora, etc.)

```csharp
// Core\Audio\Engines\PulseAudioPlaybackEngine.cs
public class PulseAudioPlaybackEngine : IPlaybackEngine
{
    private IntPtr _context;
    private IntPtr _stream;
    
    // TODO: Implement P/Invoke bindings to libpulse:
    // - pa_context_new()
    // - pa_context_connect()
    // - pa_stream_new()
    // - pa_stream_write()
    // - Volume control via pa_stream_get_volume()
}
```

**Required C Library**: `libpulse` (usually pre-installed on desktop Linux)

**Implementation Tasks**:
- [ ] P/Invoke declarations for libpulse functions
- [ ] Context creation and connection
- [ ] Playback stream setup with format negotiation
- [ ] Sample I/O buffering and writing
- [ ] Volume control and state management
- [ ] Error handling and cleanup

**Testing**: Ubuntu 20.04+ with PulseAudio enabled

##### 2. JACK Playback Engine

**Target Use Case**: Professional audio workstations, DAW integration

```csharp
// Core\Audio\Engines\JackPlaybackEngine.cs
public class JackPlaybackEngine : IPlaybackEngine
{
    private IntPtr _client;
    private IntPtr[] _ports;
    
    // TODO: Implement P/Invoke bindings to libjack:
    // - jack_client_open()
    // - jack_port_register() for L/R channels
    // - jack_connect() to physical outputs
    // - jack_process_callback for real-time thread
    // - Volume control via jack_port_set_alias()
}
```

**Required C Library**: `libjack` (installed separately on professional systems)

**Implementation Tasks**:
- [ ] P/Invoke declarations for libjack functions
- [ ] JACK client registration
- [ ] Port creation for stereo/mono output
- [ ] Connection to physical outputs
- [ ] Real-time callback implementation
- [ ] Non-blocking sample I/O
- [ ] Error handling and cleanup

**Testing**: Professional Linux workstations with JACK daemon running

##### 3. ALSA Playback Engine (Recommended for ChromeOS)

**Target Use Case**: ChromeOS Penguin container, embedded Linux systems

```csharp
// Core\Audio\Engines\AlsaPlaybackEngine.cs
public class AlsaPlaybackEngine : IPlaybackEngine
{
    private IntPtr _handle;
    private AudioFormat _format;
    
    // TODO: Implement P/Invoke bindings to libasound (ALSA):
    // - snd_pcm_open()
    // - snd_pcm_set_params()
    // - snd_pcm_writei()
    // - snd_pcm_recover()
    // - Volume control via snd_mixer API
}
```

**Required C Library**: `libasound` (always present on Linux systems)

**Implementation Tasks**:
- [ ] P/Invoke declarations for libasound functions
- [ ] PCM device opening (default or specified)
- [ ] Format/sample rate/channel configuration
- [ ] Non-blocking writes and underrun recovery
- [ ] Volume control via mixer API
- [ ] Hardware parameter negotiation
- [ ] Error handling and cleanup

**Testing**: ChromeOS Penguin container (Debian-based)

#### Recording Engines (🚧 Interface Contract Ready, Implementation Pending)

Recording engines follow the same pattern as playback engines:

- `PulseAudioRecordingEngine` - Capture streams via libpulse
- `JackRecordingEngine` - Input ports via libjack
- `AlsaRecordingEngine` - PCM input via libasound

**Implementation**: Mirror playback engine implementation with recording-specific APIs

---

### macOS (📋 Future - Not Yet Started)

**Planned Implementation**:
- `CoreAudioPlaybackEngine` - Uses CoreAudio framework
- `CoreAudioRecordingEngine` - Uses CoreAudio framework
- `CoreAudioDeviceEnumerator` - Uses AudioObjectID enumeration

**Status**: Architecture supports future implementation; not started

---

## Development Roadmap

### Phase 1: Verification (✅ Complete)
- [x] Create abstraction layer interfaces
- [x] Implement Windows engines (NAudio)
- [x] Create platform detection system
- [x] Build Linux device enumerators for all backends
- [x] Verify device enumeration works on ChromeOS

**Current Work**: Verifying device enumeration on ChromeOS Penguin

### Phase 2: ALSA Implementation (📅 Upcoming)
- [ ] Implement `AlsaPlaybackEngine` with libasound P/Invoke
- [ ] Implement `AlsaRecordingEngine` with libasound P/Invoke
- [ ] Test on ChromeOS Penguin container
- [ ] Test on minimal embedded Linux systems
- [ ] Integration with `AudioDeck` and cartwall playback

**Target Timeline**: This sprint

### Phase 3: PulseAudio Implementation (📅 Next)
- [ ] Implement `PulseAudioPlaybackEngine` with libpulse P/Invoke
- [ ] Implement `PulseAudioRecordingEngine` with libpulse P/Invoke
- [ ] Test on Ubuntu 20.04+, Fedora, Debian
- [ ] Test with various audio configurations (speakers, USB devices, HDMI)
- [ ] Integration testing with full UI

**Target Timeline**: Following phase 2

### Phase 4: JACK Implementation (📅 Later)
- [ ] Implement `JackPlaybackEngine` with libjack P/Invoke
- [ ] Implement `JackRecordingEngine` with libjack P/Invoke
- [ ] Test on professional audio systems
- [ ] Ensure real-time performance in callback
- [ ] Integration with DAWs and audio infrastructure

**Target Timeline**: Q2 2026 or later

### Phase 5: macOS Support (📅 Future)
- [ ] Research CoreAudio framework capabilities
- [ ] Design CoreAudio engine implementations
- [ ] Implement engines
- [ ] Test on macOS 10.15+

**Target Timeline**: Q3 2026 or later

---

## Implementation Guidelines

### P/Invoke Best Practices

All Linux audio engines must follow these patterns:

```csharp
// 1. Declare C library functions with proper marshaling
[DllImport("libpulse.so.0", CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr pa_context_new(...);

// 2. Use try/finally for cleanup
try
{
    _context = pa_context_new(...);
    pa_context_connect(_context, ...);
}
finally
{
    if (_context != IntPtr.Zero)
        pa_context_unref(_context);
}

// 3. Check for null pointer returns
if (result == IntPtr.Zero)
    throw new PlatformNotSupportedException("Failed to create audio context");
```

### Audio Format Standardization

All engines must support:
- **Sample Rates**: 44.1 kHz, 48 kHz (minimum)
- **Bit Depths**: 16-bit PCM (minimum), 24-bit and 32-bit recommended
- **Channels**: Mono and Stereo (minimum)
- **Format**: Linear PCM (LPCM)

### Error Handling Requirements

Each engine must:
- Throw `PlatformNotSupportedException` if audio system unavailable
- Throw `AudioException` for device errors
- Log all failures with timestamps and context
- Gracefully degrade if optional features unavailable

### State Machine Requirements

All engines must properly implement:
- `Stopped` → `Playing` (via Play)
- `Playing` → `Paused` (via Pause)
- `Paused` → `Playing` (via Play)
- Any state → `Stopped` (via Stop or end of samples)

---

## Testing Strategy

### Unit Tests

Each engine should have comprehensive tests:

```csharp
// Tests/Audio/AlsaPlaybackEngineTests.cs
public class AlsaPlaybackEngineTests
{
    [Test]
    public void Initialize_WithValidFormat_Succeeds()
    [Test]
    public void Play_WithValidSamples_ProducesAudio()
    [Test]
    public void Pause_FromPlaying_StopsAudio()
    [Test]
    public void SetVolume_WithValidRange_ChangesVolume()
    [Test]
    public void GetState_ReturnsCurrentPlaybackState()
    // ... etc
}
```

### Integration Tests

Test full workflows:
- Load track → Get format → Initialize engine → Play → Pause → Stop
- Device enumeration → Select device → Initialize → Play
- Volume control chain (get → set → get)
- Error recovery (underrun, reconnect)

### Platform-Specific Tests

- **ChromeOS Penguin**: Run ALSA tests in container
- **Desktop Linux**: Run PulseAudio and JACK tests
- **Professional Audio**: JACK real-time performance tests

### Manual Testing Checklist

- [ ] Device enumeration shows correct devices
- [ ] Playback produces audible sound from selected device
- [ ] Recording captures audio from selected microphone
- [ ] Volume control works (both programmatic and hardware)
- [ ] Pause/resume maintains playback position
- [ ] Multiple devices can be listed and selected
- [ ] Error messages are clear and helpful

---

## Integration with Existing Components

After engine implementation, the following components need updates:

### 1. AudioDeck (Playback)
```csharp
public class AudioDeck
{
    private IPlaybackEngine _playbackEngine;
    // Currently uses NAudio WaveOutEvent
    // Change to: _playbackEngine = AudioEngineFactory.CreatePlaybackEngine();
}
```

### 2. CartPlayer (Cart Playback)
```csharp
public class CartPlayer
{
    private IPlaybackEngine _playbackEngine;
    // Currently uses NAudio WaveOutEvent per cart
    // Change to use factory
}
```

### 3. MicInputService (Recording)
```csharp
public class MicInputService
{
    private IRecordingEngine _recordingEngine;
    // Currently uses NAudio WaveInEvent
    // Change to: _recordingEngine = AudioEngineFactory.CreateRecordingEngine();
}
```

### 4. WaveAudioDeviceResolver (Device Selection)
```csharp
public class WaveAudioDeviceResolver
{
    private IAudioDeviceEnumerator _enumerator;
    // Currently uses NAudio device enumeration
    // Change to: _enumerator = AudioEngineFactory.CreateDeviceEnumerator();
}
```

---

## User-Facing Features (Unchanged)

All user-facing features remain identical across platforms:

✅ **UI & Controls**:
- Device selection dropdowns
- Play/pause/stop buttons
- Volume sliders (deck, microphone, cue)
- VU meters
- Cart pad buttons and indicators

✅ **Features**:
- Full audio playback with all transport controls
- Microphone input with monitoring
- Real-time level meters (≥20 Hz refresh)
- All queue and deck features
- AutoDJ and automation with audio playback

✅ **Performance**:
- Low-latency playback
- Non-blocking operations
- Smooth deck transitions
- Real-time safe recording

---

## Build & Compilation

### Conditional Compilation Symbols

Linux engines use conditional compilation for platform-specific code:

```csharp
#if LINUX || NETCOREAPP
    // Linux-specific P/Invoke declarations
#endif
```

### Project File Configuration

The `.csproj` file already supports:
```xml
<TargetFramework>net8.0</TargetFramework>
<!-- Builds for Windows, Linux, and macOS -->
```

No changes needed; all engines compile together with platform detection at runtime.

---

## Documentation & References

### Audio System Documentation
- **ALSA (libasound)**: https://www.alsa-project.org/wiki/Main_Page
- **PulseAudio (libpulse)**: https://www.freedesktop.org/wiki/Software/PulseAudio/
- **JACK (libjack)**: https://jackaudio.org/
- **CoreAudio (macOS)**: https://developer.apple.com/documentation/coreaudio

### .NET & P/Invoke Documentation
- **P/Invoke Marshaling**: https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke
- **.NET Platform Abstraction**: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Runtime.InteropServices/

### ChromeOS Development
- **ChromeOS Linux (Crostini)**: https://support.google.com/chromebook/answer/9145439
- **Penguin Container**: https://chromium.googlesource.com/chromiumos/containers/cros-container-guest-tools

---

## FAQ

### Q: Why three backends?
**A**: Different Linux distributions and environments use different audio systems:
- **PulseAudio**: 99% of desktop Linux (Ubuntu, Fedora, Debian, GNOME)
- **JACK**: Professional audio and DAW integration
- **ALSA**: Embedded systems, containers, fallback for minimal systems

### Q: Which backend should users install?
**A**: None - the app auto-detects what's available. Most distributions come with PulseAudio pre-installed. ChromeOS has all three.

### Q: Will Windows audio still work?
**A**: Yes - completely separate code path. Windows continues using NAudio with no changes.

### Q: Can I switch backends at runtime?
**A**: Not currently. The backend is selected once on startup based on availability. Could be added as a settings option in future.

### Q: What if no audio backend is available?
**A**: App throws `PlatformNotSupportedException` with clear message. Library and other features still work without audio.

### Q: When will macOS support be ready?
**A**: After Linux is stable. macOS will use CoreAudio framework; planned for Q3 2026.

---

## Key Milestones

- ✅ **Jan 2026**: Architecture complete, ALSA detection working
- 📅 **Feb 2026**: ALSA engines implemented and tested on ChromeOS
- 📅 **Mar 2026**: PulseAudio engines implemented and tested on desktop Linux
- 📅 **Apr 2026**: Full Linux support release (v1.5.0)
- 📅 **Jun 2026**: JACK integration (optional, for professionals)
- 📅 **Sep 2026**: macOS support (CoreAudio)

---

**Questions or Issues?**

For detailed implementation specifics, see:
- [LINUX_AUDIO_IMPLEMENTATION.md](./LINUX_AUDIO_IMPLEMENTATION.md) - Technical details
- [AUDIO_ABSTRACTION_LAYER.md](./AUDIO_ABSTRACTION_LAYER.md) - Architecture details
- Code files in `OpenBroadcaster/Core/Audio/Engines/`
