using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Audio.Linux
{
    /// <summary>
    /// PulseAudio device resolver for Linux audio device enumeration.
    /// Discovers available PulseAudio sinks and presents them as AudioDeviceInfo.
    /// </summary>
    public sealed class PulseAudioDeviceResolver : IAudioDeviceResolver
    {
        private readonly ILogger<PulseAudioDeviceResolver> _logger;
        private List<AudioDeviceInfo> _cachedDevices = new();
        private DateTime _lastEnumeration = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(5);

        public PulseAudioDeviceResolver(ILogger<PulseAudioDeviceResolver>? logger = null)
        {
            if (!PlatformDetection.SupportsLinuxAudio)
            {
                throw new PlatformNotSupportedException(
                    $"PulseAudioDeviceResolver requires Linux. Running on: {PlatformDetection.ArchitectureInfo}");
            }

            _logger = logger ?? AppLogger.CreateLogger<PulseAudioDeviceResolver>();
        }

        /// <summary>
        /// Gets the list of available playback devices.
        /// Attempts to query PulseAudio; returns sensible defaults if unavailable.
        /// </summary>
        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            // Return cached results if still valid
            if (DateTime.UtcNow - _lastEnumeration < _cacheExpiration && _cachedDevices.Count > 0)
            {
                return _cachedDevices;
            }

            try
            {
                var devices = QueryPulseAudioDevices();
                if (devices.Count > 0)
                {
                    _cachedDevices = devices;
                    _lastEnumeration = DateTime.UtcNow;
                    return devices;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query PulseAudio devices, falling back to defaults");
            }

            // Fallback: return default/simulated devices
            return GetDefaultDevices();
        }

        /// <summary>
        /// Gets the list of available input/recording devices.
        /// Attempts to query PulseAudio sources.
        /// </summary>
        public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
        {
            try
            {
                var devices = QueryPulseAudioInputDevices();
                if (devices.Count > 0)
                {
                    return devices;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query PulseAudio input devices");
            }

            // Fallback: return default input devices
            return GetDefaultInputDevices();
        }

        /// <summary>
        /// Queries PulseAudio for available sinks using pactl command.
        /// </summary>
        private List<AudioDeviceInfo> QueryPulseAudioDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var process = new ProcessStartInfo
                {
                    FileName = "pactl",
                    Arguments = "list short sinks",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(process);
                if (proc == null)
                {
                    _logger.LogWarning("Could not start pactl process");
                    return devices;
                }

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000); // 5 second timeout

                if (proc.ExitCode != 0)
                {
                    _logger.LogWarning("pactl exited with code {ExitCode}", proc.ExitCode);
                    return devices;
                }

                // Parse pactl output
                // Format: "0	alsa_output.pci-0000:00:1f.3.analog-stereo	module-alsa-card.c	Analog Stereo"
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int deviceId))
                    {
                        string displayName = parts[3];
                        devices.Add(new AudioDeviceInfo(deviceId, displayName));
                        _logger.LogInformation("Enumerated PulseAudio device: {DisplayName} (ID: {DeviceId})", displayName, deviceId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while querying PulseAudio devices");
            }

            return devices;
        }

        /// <summary>
        /// Returns default/sensible audio devices when PulseAudio is unavailable.
        /// </summary>
        private List<AudioDeviceInfo> GetDefaultDevices()
        {
            return new List<AudioDeviceInfo>
            {
                new AudioDeviceInfo(0, "Default Playback Device"),
                new AudioDeviceInfo(1, "Analog Output"),
                new AudioDeviceInfo(2, "Digital Output (SPDIF)"),
                new AudioDeviceInfo(3, "HDMI Output")
            };
        }

        /// <summary>
        /// Queries PulseAudio for available input/recording sources.
        /// </summary>
        private List<AudioDeviceInfo> QueryPulseAudioInputDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var process = new ProcessStartInfo
                {
                    FileName = "pactl",
                    Arguments = "list short sources",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(process);
                if (proc == null)
                {
                    return devices;
                }

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);

                if (proc.ExitCode != 0)
                {
                    return devices;
                }

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int deviceId))
                    {
                        string displayName = parts[3];
                        devices.Add(new AudioDeviceInfo(deviceId, displayName));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while querying PulseAudio input devices");
            }

            return devices;
        }

        /// <summary>
        /// Returns default input devices when PulseAudio is unavailable.
        /// </summary>
        private List<AudioDeviceInfo> GetDefaultInputDevices()
        {
            return new List<AudioDeviceInfo>
            {
                new AudioDeviceInfo(0, "Default Microphone"),
                new AudioDeviceInfo(1, "Built-in Microphone"),
                new AudioDeviceInfo(2, "USB Microphone")
            };
        }

        /// <summary>
        /// Invalidates the device cache to force re-enumeration on next query.
        /// Useful if devices have changed at runtime.
        /// </summary>
        public void InvalidateCache()
        {
            _lastEnumeration = DateTime.MinValue;
            _logger.LogInformation("Audio device cache invalidated");
        }
    }
}
