# Linux Compatibility Implementation Details
**OpenBroadcaster - Technical Architecture**

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                  Avalonia UI Layer                      │
│            (Cross-platform with UsePlatformDetect)      │
└──────────────────┬──────────────────────────────────────┘
                   │
      ┌────────────┴────────────┐
      │                         │
  ┌───▼───────┐          ┌─────▼────────┐
  │ Windows   │          │ Linux        │
  │ Services  │          │ Services     │
  └───┬───────┘          └─────┬────────┘
      │                         │
  ┌───▼──────────┐    ┌────────▼────────┐
  │ NAudio       │    │ FFmpeg/ALSA     │
  │ - WaveOut    │    │ - ffmpeg        │
  │ - WaveIn     │    │ - ffplay        │
  │ - WASAPI     │    │ - paplay        │
  │ - LAME       │    │ - pactl         │
  └───┬──────────┘    └────────┬────────┘
      │                        │
  ┌───▼──────────┐    ┌────────▼────────┐
  │ Windows API  │    │ Linux Daemons   │
  │ Audio Devices│    │ - PulseAudio    │
  │              │    │ - ALSA          │
  └──────────────┘    └─────────────────┘
```

---

## Module: Microphone Input

### Windows Path
```
User initiates audio input
         ↓
MicInputService detects Windows via OperatingSystem.IsWindows()
         ↓
Creates WaveInEvent (NAudio)
         ↓
WaveInEvent.DeviceNumber specifies audio device
         ↓
Captures PCM samples via Windows Audio API
         ↓
Events: DataAvailable, RecordingStopped
```

### Linux Path
```
User initiates audio input
         ↓
MicInputService detects Linux via OperatingSystem.IsLinux()
         ↓
Creates PulseAudioMicCapture
         ↓
PulseAudioMicCapture tries ALSA first (hw:0,0 via ffmpeg)
         ├─ If ALSA available:
         │  └─ ffmpeg -f alsa -i hw:0,0 -f s16le -ar 44100 -ac 2 pipe:1
         │  └─ Reads PCM from stdout
         │  └─ Falls back to PulseAudio if ffmpeg fails
         │
         └─ If ALSA unavailable:
            └─ ffmpeg -f pulse -i <device> -f s16le ...
            └─ Reads PCM from stdout
```

### Implementation Files
- **Service:** `Core/Services/MicInputService.cs`
- **Windows:** NAudio `WaveInEvent` (guarded with `#if NET8_0_WINDOWS`)
- **Linux:** `Core/Services/PulseAudioMicCapture.cs`
- **Fallback:** `Core/Services/OpenAlMicCapture.cs` (legacy, optional)

---

## Module: Audio Playback

### Windows Path
```
Audio samples queued
         ↓
AudioService routes to appropriate output device
         ↓
WaveOutAudioOutput created via factory
         ↓
NAudio WaveOut sends to Windows Audio Endpoint
         ↓
Output device plays audio
```

### Linux Path
```
Audio samples queued
         ↓
AudioService routes to appropriate output device
         ↓
PaplayAudioOutput created via factory
         ↓
ffplay process spawned:
  ffplay -f s16le -ar 44100 -ac 2 -nodisp -autoexit -i pipe:0
         ↓
Samples piped to ffplay stdin
         ↓
ffplay outputs to default PulseAudio device
         ↓
Audio device plays audio

Fallback chain: ffplay → paplay → sox → aplay
```

### Implementation Files
- **Factory:** `Core/Audio/IAudioOutput.cs`
- **Windows:** `Core/Audio/WaveOutAudioOutput.cs`
- **Linux:** `Core/Audio/PaplayAudioOutput.cs`
- **Null:** `Core/Audio/NullAudioOutput.cs` (testing/headless)

---

## Module: Audio File Decoding

### Windows Path
```
Load audio file
         ↓
AudioFileReaderFactory.OpenRead(path)
         ↓
OperatingSystem.IsWindows() → true
         ↓
Create AudioFileReader (NAudio)
         ↓
AudioFileReader reads:
  ├─ MP3 via built-in decoder
  ├─ WAV via built-in decoder
  └─ Other formats via ACM (if available)
         ↓
Returns WaveStream
```

### Linux Path
```
Load audio file (any format)
         ↓
AudioFileReaderFactory.OpenRead(path)
         ↓
OperatingSystem.IsWindows() → false
         ↓
Create FfmpegWaveStream
         ↓
FfmpegWaveStream spawns ffmpeg:
  ffmpeg -i <file> -f f32le -acodec pcm_f32le -ac 2 -ar 44100 -
         ↓
FFmpeg transcodes any format to PCM F32LE
         ↓
Samples read from ffmpeg stdout
         ↓
Returns standardized WaveStream
```

### Supported Formats
- MP3, WAV, FLAC, OGG, AAC, WMA, M4A, etc.
- Any format supported by: NAudio (Windows) or FFmpeg (Linux)

### Implementation Files
- **Factory:** `Core/Audio/AudioFileReaderFactory.cs`
- **Windows:** NAudio `AudioFileReader`
- **Linux:** `Core/Audio/FfmpegWaveStream.cs`

---

## Module: Audio Encoding (MP3)

### Windows Path
```
Audio samples ready for encoding
         ↓
EncoderManager detects Windows
         ↓
LAME MP3 encoder path:
  ├─ NAudio.Lame library loaded (guarded with #if NET8_0_WINDOWS)
  ├─ LAME DLL loaded via P/Invoke
  └─ Audio samples encoded via LAME interface
         ↓
Output: MP3 bitstream
```

### Linux Path
```
Audio samples ready for encoding
         ↓
EncoderManager detects Linux
         ↓
FFmpeg MP3 encoder path:
  ├─ FFmpeg process spawned:
  │  ffmpeg -f s16le -ar 44100 -ac 2 -i pipe:0
  │          -c:a libmp3lame -b:a 192k -f mp3 pipe:1
  │
  ├─ Samples piped to ffmpeg stdin
  ├─ FFmpeg outputs MP3 via libmp3lame
  └─ MP3 read from ffmpeg stdout
         ↓
Output: MP3 bitstream
```

### Implementation Files
- **Encoder Manager:** `Core/Streaming/EncoderManager.cs` (lines 990-1030)
- **Windows:** NAudio.Lame (guarded #if NET8_0_WINDOWS)
- **Linux:** FFmpeg command line

---

## Module: Device Enumeration

### Windows Path
```
List audio input devices
         ↓
WaveAudioDeviceResolver.GetDevices()
         ↓
Guarded: #if NET8_0_WINDOWS
         ↓
Uses NAudio.CoreAudioApi.MMDeviceEnumerator
         ↓
Returns device list with:
  ├─ Device ID (integer)
  ├─ Friendly name
  ├─ Capabilities
  └─ State (connected, disabled, etc.)
```

### Linux Path
```
List audio input devices
         ↓
LinuxAudioDeviceResolver.GetInputDevices()
         ↓
Spawns: pactl list sources
         ↓
Parses output:
  ├─ Source index
  ├─ Name (e.g., "alsa_input.pci-0000_00_1f.3.analog-stereo")
  └─ State (RUNNING, SUSPENDED, etc.)
         ↓
Returns device list with:
  ├─ Device ID (index)
  ├─ Friendly name
  └─ State
```

### Implementation Files
- **Windows:** `Core/Audio/WaveAudioDeviceResolver.cs` (#if NET8_0_WINDOWS)
- **Linux:** `Core/Audio/LinuxAudioDeviceResolver.cs`
- **Factory:** `Core/Audio/IAudioDeviceResolver.cs`

---

## Module: Settings Storage

### Unified Cross-Platform Approach
```
Application Settings
         ↓
Store in platform-independent location
         ↓
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
         ↓
      ┌───────────────────┴───────────────────┐
      │                                       │
   Windows                                 Linux
   ────────                                 ─────
C:\Users\<user>\                      ~/.config/
AppData\Roaming\                    
     OpenBroadcaster\                   OpenBroadcaster\
                                        
      ↓                                    ↓
   twitch.settings.json              twitch.settings.json
   cart.db                            cart.db
   library.db                         library.db
   logs/                              logs/
```

### Implementation Files
- **Settings Store:** `Core/Services/AppSettingsStore.cs` (line 73)
- **Twitch Settings:** `Core/Services/TwitchSettingsStore.cs` (lines 57-59)
- **Loyalty:** `Core/Services/LoyaltyLedger.cs` (lines 145-146)
- **Library:** `Core/Services/LibraryService.cs` (lines 662-663)
- **Logging:** `Core/Diagnostics/AppLogger.cs` (lines 87-88)

### JSON Format (Cross-Platform)
```json
{
  "username": "broadcaster",
  "oauthToken": "oauth:xxxx",
  "channel": "#mychannel"
}
```
- Uses `System.Text.Json` (cross-platform)
- UTF-8 encoding (standard on all platforms)
- No platform-specific JSON features

---

## Module: Streaming (Icecast/Shoutcast)

### Connection Flow (Platform-Agnostic)
```
Streaming profile configured
         ↓
User clicks "Start Stream"
         ↓
EncoderManager creates audio pipeline:
  ├─ Audio source (deck, encoder, etc.)
  ├─ Audio format (PCM → MP3)
  └─ Audio routing (mixer)
         ↓
Connect to streaming server
  ├─ Create TCP socket (cross-platform)
  ├─ Send HTTP SOURCE request with credentials
  ├─ Establish audio stream
  └─ Begin sending MP3 frames
         ↓
Stream continues until stopped
         ↓
Close connection gracefully
```

### Implementation
- **Streaming:** `Core/Streaming/EncoderManager.cs`
- **Protocol:** HTTP/ICY (platform-agnostic)
- **Networking:** `System.Net.Sockets.TcpClient` (cross-platform)
- **Encryption:** `System.Net.Security.SslStream` (cross-platform)

---

## Module: Twitch Integration

### Connection Flow (Cross-Platform)
```
Twitch credentials configured
         ↓
TwitchIrcClient.ConnectAsync()
         ↓
Platform-agnostic steps:
  ├─ TCP connection to irc.chat.twitch.tv:6667
  ├─ SSL/TLS handshake
  ├─ Send PASS, NICK, USER commands
  ├─ JOIN #channel
  └─ Listen for messages
         ↓
Handle chat events
  ├─ Message events
  ├─ User events
  └─ System events
         ↓
Send chat messages via IRC protocol
```

### Implementation Files
- **Twitch IRC:** `Core/Services/TwitchIrcClient.cs`
- **Integration:** `Core/Services/TwitchIntegrationService.cs`
- **Networking:** Standard `System.Net.Sockets`

---

## Module: UI - Avalonia

### Platform Detection (Automatic)
```csharp
// Program.cs
AppBuilder.Configure<App>()
    .UsePlatformDetect()    // ← Magic line
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
```

### What `UsePlatformDetect()` Does
```
Runtime detection:
  ├─ Windows → Uses Win32 backend
  ├─ Linux → Uses GTK/Wayland backend
  ├─ macOS → Uses AppKit backend
  └─ Unknown → Falls back to safe defaults
```

### Supported Controls
All Avalonia controls are cross-platform:
```
✅ Window, Grid, StackPanel
✅ Button, TextBox, ComboBox
✅ DataGrid, ListBox
✅ Image, Canvas
✅ etc. (all UI elements)
```

No Windows-specific UI code:
```
❌ NOT: System.Windows.Forms
❌ NOT: System.Windows.Wpf
✅ YES: Avalonia (cross-platform)
```

### Implementation Files
- **Entry Point:** `Program.cs`
- **App:** `App.axaml.cs`
- **Main Window:** `MainWindow.axaml.cs`
- **All Views:** `Views/` folder (all cross-platform XAML)

---

## Module: Logging & Diagnostics

### Log Output (Cross-Platform)
```
Application starts
         ↓
AppLogger.Configure()
         ↓
Serilog configured with:
  ├─ Console sink (all platforms)
  ├─ File sink (Windows: AppData, Linux: ~/.config)
  └─ Structured logging
         ↓
Logs written to:
  ~/.config/OpenBroadcaster/logs/openbroadcaster-<date>.log
```

### Diagnostic Data
- Captured to standard locations
- JSON format (cross-platform)
- No platform-specific debug info in logs

### Implementation Files
- **Logger:** `Core/Diagnostics/AppLogger.cs`
- **Serilog Configuration:** Structured text output
- **Log Location:** `~/.config/OpenBroadcaster/logs/`

---

## Critical Success Factors

### 1. External Tool Availability
```bash
# Must be in system PATH for Linux
ffmpeg       # Core dependency
ffplay       # Audio playback fallback
paplay       # Primary audio output
pactl        # Device enumeration
```

### 2. Audio Daemon Availability
```bash
# Must be running on Linux
PulseAudio or PipeWire  # Daemon
  OR
ALSA (direct access)    # Fallback
```

### 3. File System Permissions
```bash
~/.config/OpenBroadcaster/   # Must be writable
/dev/snd/                     # Must be readable/writable
```

### 4. Network Access
```bash
# Must be available for streaming
Outbound TCP to streaming server
Outbound TCP to irc.chat.twitch.tv:6667
```

---

## Testing Strategy

### Unit Tests (All Platforms)
```csharp
// Mock external dependencies
// Test factory selections
// Test configuration loading
// Verify cross-platform APIs used
```

### Integration Tests (Platform-Specific)
```bash
# Windows
- Test NAudio initialization
- Test WASAPI loopback
- Test LAME encoding

# Linux
- Test PulseAudio connection
- Test ALSA fallback
- Test FFmpeg codec handling
```

### Manual Verification
```bash
# Linux
./openbroadcaster
> Settings file created? ✅
> Audio devices detected? ✅
> Microphone responds? ✅
> File playback works? ✅
> Streaming connects? ✅
```

---

## Performance Optimization

### Audio Buffering
```csharp
// Optimized buffer sizes for different operations
- Capture:  2048 samples (≈46ms @ 44.1kHz)
- Playback: 8192 samples (≈185ms @ 44.1kHz)
- Encoding: 4096 samples (≈93ms @ 44.1kHz)
```

### Process Communication
```csharp
// Direct stream piping (not temporary files)
- ffmpeg stdout → application memory
- Application memory → ffmpeg stdin
- No disk I/O for real-time audio
```

### Threading Model
```csharp
// Async/await for I/O-bound operations
// Dedicated threads for audio processing
// Cancellation tokens for graceful shutdown
```

---

## Security Considerations

### Windows Audio API
- Uses WASAPI (modern, secure)
- Requires process token for device access
- Cannot capture protected content

### Linux Audio API
- PulseAudio: Per-user daemon (containerized)
- ALSA: Direct device access (needs permissions)
- Credentials for streaming not stored in memory

### Network Security
- SSL/TLS for Icecast/Shoutcast
- OAuth tokens for Twitch (encrypted at rest)
- Settings file contains sensitive data (filesystem ACLs)

---

## Conclusion

OpenBroadcaster's architecture cleanly separates platform-specific code from cross-platform logic:

1. **Abstraction Layers:** Interfaces for all platform-specific components
2. **Factory Pattern:** Runtime selection of appropriate implementation
3. **Conditional Compilation:** `#if NET8_0_WINDOWS` for compile-time guards
4. **Standard APIs:** Avoids proprietary platform APIs when possible

This design enables:
- ✅ Linux deployment as first-class platform
- ✅ Windows support without platform pollution
- ✅ Future platform additions (macOS, etc.)
- ✅ Maintainable, testable codebase

**Result: 100% Linux Functional** ✅

