# OpenBroadcaster Audio Abstraction Layer

## Overview

The audio abstraction layer provides cross-platform audio support for Windows and Linux. It automatically detects available audio backends on Linux and selects the best available option (PulseAudio > JACK > ALSA).

## Architecture

### Core Interfaces

#### `IPlaybackEngine`
Handles audio output/playback across all platforms.

**Location**: `OpenBroadcaster.Core.Audio.IPlaybackEngine`

**Methods**:
- `Init(ISampleProvider sampleProvider)` - Initialize with audio data
- `Play()` - Start playback
- `Pause()` - Pause playback
- `Stop()` - Stop playback
- `PlaybackState` (property) - Get current playback state
- `Volume` (property) - Get/set playback volume (0.0-1.0)

**Event**:
- `PlaybackStopped` - Fired when playback stops

#### `IRecordingEngine`
Handles audio input/recording across all platforms.

**Location**: `OpenBroadcaster.Core.Audio.IRecordingEngine`

**Methods**:
- `StartRecording(int deviceNumber)` - Start recording from device
- `StopRecording()` - Stop recording
- `Volume` (property) - Get/set recording volume (0.0-1.0)

**Events**:
- `SamplesAvailable` - Fired when audio samples are available (MicSampleBlockEventArgs)
- `LevelChanged` - Fired when recording level changes (for VU metering)

#### `IAudioDeviceEnumerator`
Lists available audio devices.

**Location**: `OpenBroadcaster.Core.Audio.IAudioDeviceEnumerator`

**Methods**:
- `GetPlaybackDevices()` - Get list of output devices
- `GetRecordingDevices()` - Get list of input devices
- `BackendName` (property) - Get the audio backend name (e.g., "PulseAudio", "ALSA")

### Platform Implementations

#### Windows (All Audio Operations)
Uses NAudio library (WaveOut, WaveIn):
- `WindowsPlaybackEngine` - NAudio WaveOutEvent
- `WindowsRecordingEngine` - NAudio WaveInEvent
- `WindowsAudioDeviceEnumerator` - NAudio device enumeration

**Location**: `OpenBroadcaster.Core.Audio.Engines\Windows*.cs`

#### Linux (Auto-detecting best backend)

##### PulseAudio (Desktop Linux - Preferred)
- `PulseAudioPlaybackEngine` - PulseAudio output (stub with TODO for full impl)
- `PulseAudioRecordingEngine` - PulseAudio input (stub with TODO for full impl)
- `PulseAudioDeviceEnumerator` - Parse `pactl list short sinks/sources`

**Location**: `OpenBroadcaster.Core.Audio.Engines\PulseAudio*.cs`

**Requirements**:
- `pactl` command available (part of PulseAudio)
- PulseAudio daemon running

##### JACK (Professional Audio)
- `JackPlaybackEngine` - JACK output (stub with TODO for full impl)
- `JackRecordingEngine` - JACK input (stub with TODO for full impl)
- `JackAudioDeviceEnumerator` - Parse `jack_lsp -p` output

**Location**: `OpenBroadcaster.Core.Audio.Engines\Jack*.cs`

**Requirements**:
- `jack_lsp` command available
- JACK audio server running

##### ALSA (Fallback - Always Available)
- `AlsaPlaybackEngine` - ALSA output (stub with TODO for full impl)
- `AlsaRecordingEngine` - ALSA input (stub with TODO for full impl)
- `AlsaDeviceEnumerator` - Parse `aplay -l` and `arecord -l` output

**Location**: `OpenBroadcaster.Core.Audio.Engines\Alsa*.cs`

**Requirements**:
- `aplay` and `arecord` commands available (part of ALSA utils)

### Audio Backend Detection

**Location**: `OpenBroadcaster.Core.Audio.Engines.LinuxAudioDetector`

Automatically probes for available audio backends in this order:
1. **PulseAudio** - Most common on desktop Linux
2. **JACK** - Professional audio workstations
3. **ALSA** - Always present as fallback

Methods:
- `DetectBestBackend()` - Returns the best available backend
- `IsPulseAudioAvailable()` - Check PulseAudio via `pactl --version`
- `IsJackAvailable()` - Check JACK via `jack_lsp` existence
- `IsAlsaAvailable()` - Check ALSA via `aplay --version`

### Audio Engine Factory

**Location**: `OpenBroadcaster.Core.Audio.Engines.AudioEngineFactory`

Static factory class that creates appropriate platform-specific engines:

```csharp
IPlaybackEngine engine = AudioEngineFactory.CreatePlaybackEngine();
IRecordingEngine mic = AudioEngineFactory.CreateRecordingEngine();
IAudioDeviceEnumerator devices = AudioEngineFactory.CreateDeviceEnumerator();
```

**On Windows**: Always returns Windows implementations

**On Linux**: Detects best backend and returns appropriate implementations

## Usage Example

```csharp
// Create engines (automatically selects best platform/backend)
var playbackEngine = AudioEngineFactory.CreatePlaybackEngine();
var deviceEnumerator = AudioEngineFactory.CreateDeviceEnumerator();

// List devices
var outputDevices = deviceEnumerator.GetPlaybackDevices();
foreach (var device in outputDevices)
{
    Console.WriteLine($"{device.DeviceNumber}: {device.ProductName}");
}

// Initialize and play audio
var provider = new WaveFileReader("song.wav");
playbackEngine.Init(provider);
playbackEngine.Play();

// Control volume
playbackEngine.Volume = 0.8f;
```

## Current Status

### ✅ Completed
- Core abstraction interfaces (IPlaybackEngine, IRecordingEngine, IAudioDeviceEnumerator)
- Windows implementations (full, using NAudio)
- Linux audio backend detection (PulseAudio, JACK, ALSA)
- Linux device enumerators (command-line parsing)
- Platform detection (Windows, Linux, macOS)
- Factory pattern for cross-platform engine creation

### ⏳ In Progress / TODO

#### PulseAudio Engines
- `PulseAudioPlaybackEngine` - Implement actual audio playback
  - Use libpulse C library via P/Invoke
  - Handle stream creation and sample submission
  - Implement volume control
  
- `PulseAudioRecordingEngine` - Implement actual audio recording
  - Use libpulse C library via P/Invoke
  - Handle stream creation and sample capture
  - Provide sample data to SamplesAvailable event

#### JACK Engines
- `JackPlaybackEngine` - Implement actual JACK playback
  - Use libjack C library via P/Invoke
  - Handle JACK client creation and port connections
  - Implement real-time audio thread
  
- `JackRecordingEngine` - Implement actual JACK recording
  - Use libjack C library via P/Invoke
  - Handle JACK client creation and port connections
  - Capture samples from JACK graph

#### ALSA Engines
- `AlsaPlaybackEngine` - Implement actual ALSA playback
  - Use libasound (ALSA) C library via P/Invoke
  - Handle PCM device opening and configuration
  - Implement sample submission loop
  
- `AlsaRecordingEngine` - Implement actual ALSA recording
  - Use libasound (ALSA) C library via P/Invoke
  - Handle PCM device opening and configuration
  - Implement sample capture loop

#### Integration
- Update `AudioDeck` to use `IPlaybackEngine`
- Update `CartPlayer` to use `IPlaybackEngine`
- Update `MicInputService` to use `IRecordingEngine`
- Update `WaveAudioDeviceResolver` to use `IAudioDeviceEnumerator`

#### Testing
- Unit tests for backend detection
- Integration tests for device enumeration
- Cross-platform testing (Windows, Linux with ChromeOS Penguin)

## Platform Support

| Platform | Status | Audio Backends |
|----------|--------|---|
| Windows | ✅ Full | NAudio (WASAPI) |
| Linux | 🚧 Partial | PulseAudio (planned), JACK (planned), ALSA (planned) |
| macOS | ❌ Not Started | CoreAudio (future) |
| ChromeOS (Penguin) | ⏳ Testing | ALSA, PulseAudio |

## Linux Audio Testing Environment

### ChromeOS with Penguin Container
- Base: Debian Bullseye
- Audio Support: ALSA + PulseAudio (via Crostini bridge)
- Testing Strategy:
  1. Test device enumeration on each backend
  2. Test audio playback on each backend
  3. Test audio recording on each backend
  4. Verify volume control
  5. Verify proper error handling when backend unavailable

### Required Dependencies
```bash
# ALSA (usually pre-installed)
sudo apt-get install alsa-utils

# PulseAudio (for desktop Linux)
sudo apt-get install pulseaudio pulseaudio-utils

# JACK (optional, for professional audio)
sudo apt-get install jackd2 jack-tools
```

## Next Steps

1. **Priority 1**: Test device enumeration on ChromeOS Penguin
2. **Priority 2**: Implement ALSA engines (most reliable on Linux)
3. **Priority 3**: Implement PulseAudio engines (most common on desktop)
4. **Priority 4**: Implement JACK engines (optional, for pro audio)
5. **Priority 5**: Integrate with existing audio pipeline
6. **Priority 6**: Add comprehensive error handling and logging

## File Structure

```
OpenBroadcaster.Core/
├── Audio/
│   ├── Engines/
│   │   ├── AudioEngineFactory.cs
│   │   ├── LinuxAudioDetector.cs
│   │   ├── WindowsPlaybackEngine.cs
│   │   ├── WindowsRecordingEngine.cs
│   │   ├── WindowsAudioDeviceEnumerator.cs
│   │   ├── PulseAudioPlaybackEngine.cs
│   │   ├── PulseAudioRecordingEngine.cs
│   │   ├── PulseAudioDeviceEnumerator.cs
│   │   ├── JackPlaybackEngine.cs
│   │   ├── JackRecordingEngine.cs
│   │   ├── JackAudioDeviceEnumerator.cs
│   │   ├── AlsaPlaybackEngine.cs
│   │   ├── AlsaRecordingEngine.cs
│   │   └── AlsaDeviceEnumerator.cs
│   ├── IPlaybackEngine.cs
│   ├── IRecordingEngine.cs
│   └── IAudioDeviceEnumerator.cs
└── Diagnostics/
    └── PlatformDetection.cs (updated with SupportsAudio property)
```

## References

- NAudio Documentation: https://github.com/naudio/NAudio
- PulseAudio: https://wiki.freedesktop.org/wiki/Software/PulseAudio/
- JACK Audio Connection Kit: https://jackaudio.org/
- ALSA (Advanced Linux Sound Architecture): https://www.alsa-project.org/
- ChromeOS Penguin (Crostini) Linux Container: https://support.google.com/chromebook/answer/9145439
