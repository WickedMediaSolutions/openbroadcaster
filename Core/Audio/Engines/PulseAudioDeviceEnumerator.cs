using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// PulseAudio-based device enumerator for Linux.
    /// Uses `pactl` to enumerate audio devices.
    /// </summary>
    public sealed class PulseAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        public string BackendName => "PulseAudio";

        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var output = RunPactlCommand("list short sinks");
                if (string.IsNullOrEmpty(output))
                    return devices;

                // Parse output: "0	alsa_output.pci-0000_00_1b.0.analog-stereo	module-alsa-card.c	..."
                int deviceIndex = 0;
                foreach (var line in output.Split('\n'))
                {
                    var parts = line.Split('\t');
                    if (parts.Length < 2)
                        continue;

                    if (int.TryParse(parts[0], out int sinkIndex))
                    {
                        string name = parts.Length > 1 ? parts[1] : $"Sink {sinkIndex}";
                        devices.Add(new AudioDeviceInfo(deviceIndex++, name));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating PulseAudio playback devices: {ex.Message}");
            }

            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetRecordingDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var output = RunPactlCommand("list short sources");
                if (string.IsNullOrEmpty(output))
                    return devices;

                // Parse output: "0	alsa_input.pci-0000_00_1b.0.analog-stereo	module-alsa-card.c	..."
                int deviceIndex = 0;
                foreach (var line in output.Split('\n'))
                {
                    var parts = line.Split('\t');
                    if (parts.Length < 2)
                        continue;

                    if (int.TryParse(parts[0], out int sourceIndex))
                    {
                        string name = parts.Length > 1 ? parts[1] : $"Source {sourceIndex}";
                        devices.Add(new AudioDeviceInfo(deviceIndex++, name));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating PulseAudio recording devices: {ex.Message}");
            }

            return devices;
        }

        private static string RunPactlCommand(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pactl",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return "";

                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        return "";
                    }

                    return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd() : "";
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
