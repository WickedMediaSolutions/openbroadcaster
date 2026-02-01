using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// Detects available audio backends on Linux.
    /// Probes for PulseAudio, ALSA, and JACK in order of preference.
    /// </summary>
    public static class LinuxAudioDetector
    {
        public enum AudioBackend
        {
            None,
            PulseAudio,
            ALSA,
            JACK
        }

        /// <summary>
        /// Detects the best available audio backend on this Linux system.
        /// Returns in order of preference: PulseAudio, JACK, ALSA, None
        /// </summary>
        public static AudioBackend DetectBestBackend()
        {
            if (!PlatformDetection.IsLinux)
                return AudioBackend.None;

            // Check PulseAudio first (most common on desktop)
            if (IsPulseAudioAvailable())
                return AudioBackend.PulseAudio;

            // Check JACK second (professional audio)
            if (IsJackAvailable())
                return AudioBackend.JACK;

            // Fall back to ALSA (always available)
            if (IsAlsaAvailable())
                return AudioBackend.ALSA;

            return AudioBackend.None;
        }

        /// <summary>
        /// Checks if PulseAudio is available and running.
        /// </summary>
        public static bool IsPulseAudioAvailable()
        {
            try
            {
                // Check if pactl command is available
                var result = RunCommand("pactl", "--version");
                return result.success && result.output.Contains("pulseaudio");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if JACK audio server is available and running.
        /// </summary>
        public static bool IsJackAvailable()
        {
            try
            {
                // Check if jack_lsp command is available
                var result = RunCommand("jack_lsp", "", 2000);
                // jack_lsp succeeds if Jack is running, fails if not
                return result.success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if ALSA is available (always present on Linux).
        /// </summary>
        public static bool IsAlsaAvailable()
        {
            try
            {
                // ALSA utilities should always be present
                var result = RunCommand("aplay", "--version");
                return result.success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Runs a shell command and returns success status and output.
        /// </summary>
        private static (bool success, string output) RunCommand(string command, string args, int timeoutMs = 5000)
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
                        return (false, "");

                    if (!process.WaitForExit(timeoutMs))
                    {
                        process.Kill();
                        return (false, "");
                    }

                    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    return (process.ExitCode == 0, output);
                }
            }
            catch
            {
                return (false, "");
            }
        }
    }
}
