using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// JACK audio server device enumerator for Linux.
    /// Uses `jack_lsp` to enumerate JACK ports and devices.
    /// </summary>
    public sealed class JackAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        public string BackendName => "JACK Audio Server";

        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var output = RunCommand("jack_lsp", "-p");
                if (string.IsNullOrEmpty(output))
                    return devices;

                // Parse jack_lsp output for playback ports (system:playback_*)
                int index = 0;
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("system:playback_") || line.Contains("playback"))
                    {
                        string name = line.Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            devices.Add(new AudioDeviceInfo(index++, name));
                        }
                    }
                }

                // If no ports found, return default JACK device
                if (devices.Count == 0)
                {
                    devices.Add(new AudioDeviceInfo(0, "JACK System Default Output"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating JACK playback devices: {ex.Message}");
            }

            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetRecordingDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var output = RunCommand("jack_lsp", "-p");
                if (string.IsNullOrEmpty(output))
                    return devices;

                // Parse jack_lsp output for recording ports (system:capture_*)
                int index = 0;
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("system:capture_") || line.Contains("capture"))
                    {
                        string name = line.Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            devices.Add(new AudioDeviceInfo(index++, name));
                        }
                    }
                }

                // If no ports found, return default JACK device
                if (devices.Count == 0)
                {
                    devices.Add(new AudioDeviceInfo(0, "JACK System Default Input"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating JACK recording devices: {ex.Message}");
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
    }
}
