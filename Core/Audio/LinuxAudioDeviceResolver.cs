using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class LinuxAudioDeviceResolver : IAudioDeviceResolver
    {
        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var openAlDevices = OpenAlDeviceLookup.GetPlaybackDevices();
            if (openAlDevices.Count > 0)
            {
                return openAlDevices.Select((name, index) => new AudioDeviceInfo(index, name)).ToList();
            }

            var devices = TryPulseDevices("sinks");
            if (devices.Count == 0)
            {
                devices = TryAlsaDevices("aplay");
            }

            if (devices.Count == 0)
            {
                devices = FallbackDefault("Default Output");
            }

            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
        {
            var openAlDevices = OpenAlDeviceLookup.GetCaptureDevices();
            if (openAlDevices.Count > 0)
            {
                return openAlDevices.Select((name, index) => new AudioDeviceInfo(index, name)).ToList();
            }

            var devices = TryPulseDevices("sources");
            if (devices.Count == 0)
            {
                devices = TryAlsaDevices("arecord");
            }

            if (devices.Count == 0)
            {
                devices = FallbackDefault("Default Input");
            }

            return devices;
        }

        private static List<AudioDeviceInfo> TryPulseDevices(string listType)
        {
            try
            {
                var output = RunProcess("pactl", new[] { "list", "short", listType });
                if (string.IsNullOrWhiteSpace(output))
                {
                    return new List<AudioDeviceInfo>();
                }

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var devices = new List<AudioDeviceInfo>();
                foreach (var line in lines)
                {
                    var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                    {
                        index = devices.Count;
                    }

                    var name = parts[1].Trim();
                    devices.Add(new AudioDeviceInfo(index, name));
                }

                return devices;
            }
            catch
            {
                return new List<AudioDeviceInfo>();
            }
        }

        private static List<AudioDeviceInfo> TryAlsaDevices(string command)
        {
            try
            {
                var output = RunProcess(command, new[] { "-l" });
                if (string.IsNullOrWhiteSpace(output))
                {
                    return new List<AudioDeviceInfo>();
                }

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var devices = new List<AudioDeviceInfo>();
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("card ", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    devices.Add(new AudioDeviceInfo(devices.Count, trimmed));
                }

                return devices;
            }
            catch
            {
                return new List<AudioDeviceInfo>();
            }
        }

        private static List<AudioDeviceInfo> FallbackDefault(string label)
        {
            return new List<AudioDeviceInfo> { new AudioDeviceInfo(0, label) };
        }

        private static string RunProcess(string fileName, IEnumerable<string> args)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            foreach (var arg in args)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1500);
            return output;
        }
    }
}
