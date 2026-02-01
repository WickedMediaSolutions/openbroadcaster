using System;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// Factory for creating platform-specific audio engines.
    /// Automatically detects the operating system and returns appropriate implementations.
    /// </summary>
    public static class AudioEngineFactory
    {
        /// <summary>
        /// Creates a playback engine appropriate for the current platform.
        /// </summary>
        public static IPlaybackEngine CreatePlaybackEngine()
        {
            if (PlatformDetection.IsWindows)
            {
                return new WindowsPlaybackEngine();
            }
            else if (PlatformDetection.IsLinux)
            {
                return CreateLinuxPlaybackEngine();
            }
            else if (PlatformDetection.IsMacOS)
            {
                // TODO: Implement macOS CoreAudio engine
                throw new NotSupportedException("Audio playback is not yet supported on macOS");
            }
            else
            {
                throw new NotSupportedException($"Audio playback is not supported on {PlatformDetection.PlatformName}");
            }
        }

        /// <summary>
        /// Creates a recording engine appropriate for the current platform.
        /// </summary>
        public static IRecordingEngine CreateRecordingEngine()
        {
            if (PlatformDetection.IsWindows)
            {
                return new WindowsRecordingEngine();
            }
            else if (PlatformDetection.IsLinux)
            {
                return CreateLinuxRecordingEngine();
            }
            else if (PlatformDetection.IsMacOS)
            {
                // TODO: Implement macOS CoreAudio engine
                throw new NotSupportedException("Audio recording is not yet supported on macOS");
            }
            else
            {
                throw new NotSupportedException($"Audio recording is not supported on {PlatformDetection.PlatformName}");
            }
        }

        /// <summary>
        /// Creates a device enumerator appropriate for the current platform.
        /// </summary>
        public static IAudioDeviceEnumerator CreateDeviceEnumerator()
        {
            if (PlatformDetection.IsWindows)
            {
                return new WindowsAudioDeviceEnumerator();
            }
            else if (PlatformDetection.IsLinux)
            {
                return CreateLinuxDeviceEnumerator();
            }
            else if (PlatformDetection.IsMacOS)
            {
                // TODO: Implement macOS CoreAudio enumerator
                throw new NotSupportedException("Audio device enumeration is not yet supported on macOS");
            }
            else
            {
                throw new NotSupportedException($"Audio device enumeration is not supported on {PlatformDetection.PlatformName}");
            }
        }

        /// <summary>
        /// Creates a Linux playback engine based on available audio backends.
        /// Probes for PulseAudio, JACK, or ALSA (in that order) and returns the first available.
        /// </summary>
        private static IPlaybackEngine CreateLinuxPlaybackEngine()
        {
            var backend = LinuxAudioDetector.DetectBestBackend();

            return backend switch
            {
                LinuxAudioDetector.AudioBackend.PulseAudio => new PulseAudioPlaybackEngine(),
                LinuxAudioDetector.AudioBackend.JACK => new JackPlaybackEngine(),
                LinuxAudioDetector.AudioBackend.ALSA => new AlsaPlaybackEngine(),
                _ => throw new NotSupportedException("No supported audio backend found on this Linux system (checked: PulseAudio, JACK, ALSA)")
            };
        }

        /// <summary>
        /// Creates a Linux recording engine based on available audio backends.
        /// Probes for PulseAudio, JACK, or ALSA (in that order) and returns the first available.
        /// </summary>
        private static IRecordingEngine CreateLinuxRecordingEngine()
        {
            var backend = LinuxAudioDetector.DetectBestBackend();

            return backend switch
            {
                LinuxAudioDetector.AudioBackend.PulseAudio => new PulseAudioRecordingEngine(),
                LinuxAudioDetector.AudioBackend.JACK => new JackRecordingEngine(),
                LinuxAudioDetector.AudioBackend.ALSA => new AlsaRecordingEngine(),
                _ => throw new NotSupportedException("No supported audio backend found on this Linux system (checked: PulseAudio, JACK, ALSA)")
            };
        }

        /// <summary>
        /// Creates a Linux device enumerator based on available audio backends.
        /// Probes for PulseAudio, JACK, or ALSA (in that order) and returns the first available.
        /// </summary>
        private static IAudioDeviceEnumerator CreateLinuxDeviceEnumerator()
        {
            var backend = LinuxAudioDetector.DetectBestBackend();

            return backend switch
            {
                LinuxAudioDetector.AudioBackend.PulseAudio => new PulseAudioDeviceEnumerator(),
                LinuxAudioDetector.AudioBackend.JACK => new JackAudioDeviceEnumerator(),
                LinuxAudioDetector.AudioBackend.ALSA => new AlsaDeviceEnumerator(),
                _ => new AlsaDeviceEnumerator() // Fallback to ALSA, always present
            };
        }
    }
}
