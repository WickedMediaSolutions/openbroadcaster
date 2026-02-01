using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// ALSA-based device enumerator for Linux.
    /// Uses `aplay -l` and `arecord -l` to enumerate devices.
    /// </summary>
    public sealed class AlsaDeviceEnumerator : IAudioDeviceEnumerator
    {
        public string BackendName => "ALSA";

        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var output = RunCommand("aplay", "-l");
                if (string.IsNullOrEmpty(output))
                    return devices;

                // Parse aplay -l output
                int index = 0;
                foreach (var line in output.Split('\n'))
                {
                    // Expected format: "card 0: Intel [HDA Intel], device 0: ALC1220 Analog [ALC1220 Analog]"
                    if (line.Contains("card"))
                    {
                        string name = ExtractDeviceName(line);
                        if (!string.IsNullOrEmpty(name))
                        {
                            devices.Add(new AudioDeviceInfo(index++, name));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating ALSA playback devices: {ex.Message}");
            }

            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetRecordingDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var output = RunCommand("arecord", "-l");
                if (string.IsNullOrEmpty(output))
                    return devices;

                // Parse arecord -l output
                int index = 0;
                foreach (var line in output.Split('\n'))
                {
                    // Expected format: "card 0: Intel [HDA Intel], device 0: ALC1220 Analog [ALC1220 Analog]"
                    if (line.Contains("card"))
                    {
                        string name = ExtractDeviceName(line);
                        if (!string.IsNullOrEmpty(name))
                        {
                            devices.Add(new AudioDeviceInfo(index++, name));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating ALSA recording devices: {ex.Message}");
            }

            return devices;
        }

        private static string RunCommand(string command, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
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

        private static string ExtractDeviceName(string line)
        {
            // Try to extract device name from ALSA format
            // Format: "card 0: Intel [HDA Intel], device 0: ALC1220 Analog [ALC1220 Analog]"
            try
            {
                int bracketStart = line.IndexOf('[');
                int bracketEnd = line.LastIndexOf(']');
                if (bracketStart >= 0 && bracketEnd > bracketStart)
                {
                    return line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                }
            }
            catch
            {
            }

            return line;
        }
    }
}
