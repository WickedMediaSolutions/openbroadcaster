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
    /// ALSA device resolver for Linux audio device enumeration (fallback).
    /// Discovers available ALSA devices and presents them as AudioDeviceInfo.
    /// Used when PulseAudio is unavailable.
    /// </summary>
    public sealed class AlsaDeviceResolver : IAudioDeviceResolver
    {
        private readonly ILogger<AlsaDeviceResolver> _logger;
        private List<AudioDeviceInfo> _cachedDevices = new();
        private DateTime _lastEnumeration = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(5);

        public AlsaDeviceResolver(ILogger<AlsaDeviceResolver>? logger = null)
        {
            if (!PlatformDetection.SupportsLinuxAudio)
            {
                throw new PlatformNotSupportedException(
                    $"AlsaDeviceResolver requires Linux. Running on: {PlatformDetection.ArchitectureInfo}");
            }

            _logger = logger ?? AppLogger.CreateLogger<AlsaDeviceResolver>();
        }

        /// <summary>
        /// Gets the list of available playback devices.
        /// Attempts to query ALSA; returns sensible defaults if unavailable.
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
                var devices = QueryAlsaDevices();
                if (devices.Count > 0)
                {
                    _cachedDevices = devices;
                    _lastEnumeration = DateTime.UtcNow;
                    return devices;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query ALSA devices, falling back to defaults");
            }

            // Fallback: return default/sensible devices
            return GetDefaultDevices();
        }

        /// <summary>
        /// Gets the list of available input/recording devices.
        /// Attempts to query ALSA recording devices.
        /// </summary>
        public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
        {
            try
            {
                var devices = QueryAlsaInputDevices();
                if (devices.Count > 0)
                {
                    return devices;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query ALSA input devices");
            }

            // Fallback: return default input devices
            return GetDefaultInputDevices();
        }

        /// <summary>
        /// Queries ALSA for available PCM devices using aplay command.
        /// </summary>
        private List<AudioDeviceInfo> QueryAlsaDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var process = new ProcessStartInfo
                {
                    FileName = "aplay",
                    Arguments = "-l",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(process);
                if (proc == null)
                {
                    _logger.LogWarning("Could not start aplay process");
                    return devices;
                }

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000); // 5 second timeout

                if (proc.ExitCode != 0)
                {
                    _logger.LogWarning("aplay exited with code {ExitCode}", proc.ExitCode);
                    return devices;
                }

                int deviceId = 0;
                string? currentCard = null;

                // Parse aplay output
                // Format: "card 0: PCH [HDA Intel PCH], device 0: ALC256 Analog [ALC256 Analog]"
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("card "))
                    {
                        // Extract card information
                        // card 0: PCH [HDA Intel PCH], device 0: ALC256 Analog [ALC256 Analog]
                        var match = System.Text.RegularExpressions.Regex.Match(
                            line,
                            @"card (\d+):\s*(\w+)\s*\[([^\]]+)\]");

                        if (match.Success)
                        {
                            currentCard = match.Groups[3].Value; // Device description
                            deviceId = int.Parse(match.Groups[1].Value);

                            devices.Add(new AudioDeviceInfo(deviceId, currentCard));

                            _logger.LogInformation("Enumerated ALSA device: {DisplayName} (ID: {DeviceId})", currentCard, deviceId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while querying ALSA devices");
            }

            return devices;
        }

        /// <summary>
        /// Returns default/sensible audio devices when ALSA enumeration fails.
        /// </summary>
        private List<AudioDeviceInfo> GetDefaultDevices()
        {
            return new List<AudioDeviceInfo>
            {
                new AudioDeviceInfo(0, "Default ALSA Device"),
                new AudioDeviceInfo(1, "Primary PCM (hw:0,0)"),
                new AudioDeviceInfo(2, "Secondary PCM (hw:1,0)"),
                new AudioDeviceInfo(3, "USB Audio Device")
            };
        }

        /// <summary>
        /// Queries ALSA for available recording/input devices using arecord command.
        /// </summary>
        private List<AudioDeviceInfo> QueryAlsaInputDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var process = new ProcessStartInfo
                {
                    FileName = "arecord",
                    Arguments = "-l",
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

                int deviceId = 0;
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("card "))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(
                            line,
                            @"card (\d+):\s*(\w+)\s*\[([^\]]+)\]");

                        if (match.Success)
                        {
                            string displayName = match.Groups[3].Value;
                            deviceId = int.Parse(match.Groups[1].Value);

                            devices.Add(new AudioDeviceInfo(deviceId, displayName));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while querying ALSA input devices");
            }

            return devices;
        }

        /// <summary>
        /// Returns default input devices when ALSA enumeration fails.
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
