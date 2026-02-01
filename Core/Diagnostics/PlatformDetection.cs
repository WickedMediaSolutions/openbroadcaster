using System;
using System.Runtime.InteropServices;

namespace OpenBroadcaster.Core.Diagnostics
{
    /// <summary>
    /// Cross-platform detection and capability checking utility.
    /// Used to determine platform-specific feature availability at runtime.
    /// </summary>
    public static class PlatformDetection
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Gets the platform name for logging and debugging.
        /// </summary>
        public static string PlatformName =>
            IsWindows ? "Windows" :
            IsLinux ? "Linux" :
            IsMacOS ? "macOS" :
            "Unknown";

        /// <summary>
        /// Checks if running on Windows (primary supported platform for audio).
        /// </summary>
        public static bool SupportsWindowsAudio => IsWindows;

        /// <summary>
        /// Checks if audio is supported on this platform.
        /// Windows: Always supported
        /// Linux: Supported (PulseAudio, JACK, ALSA)
        /// macOS: Not yet implemented
        /// </summary>
        public static bool SupportsAudio => IsWindows || IsLinux;

        /// <summary>
        /// Gets architecture information for debugging.
        /// </summary>
        public static string ArchitectureInfo =>
            $"{PlatformName} {RuntimeInformation.OSArchitecture} ({RuntimeInformation.RuntimeIdentifier})";
    }
}
