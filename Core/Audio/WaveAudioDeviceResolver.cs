using System;
using System.Collections.Generic;
using NAudio.Wave;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Windows-only audio device resolver using NAudio WaveOut/WaveIn.
    /// On non-Windows platforms, returns an empty list.
    /// </summary>
    public sealed class WaveAudioDeviceResolver : IAudioDeviceResolver
    {
        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            if (!OperatingSystem.IsWindows())
            {
                System.Diagnostics.Trace.WriteLine($"Audio device enumeration not supported on {PlatformDetection.PlatformName}");
                return devices;
            }

            try
            {
                var deviceCount = WaveInterop.waveOutGetNumDevs();
                var capsSize = System.Runtime.InteropServices.Marshal.SizeOf<WaveOutCapabilities>();
                for (var i = 0; i < deviceCount; i++)
                {
                    if (WaveInterop.waveOutGetDevCaps((IntPtr)i, out var caps, capsSize) == NAudio.MmResult.NoError)
                    {
                        devices.Add(new AudioDeviceInfo(i, caps.ProductName));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating playback devices: {ex.Message}");
            }
            
            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            if (!OperatingSystem.IsWindows())
            {
                System.Diagnostics.Trace.WriteLine($"Audio device enumeration not supported on {PlatformDetection.PlatformName}");
                return devices;
            }

            try
            {
                var deviceCount = WaveInterop.waveInGetNumDevs();
                var capsSize = System.Runtime.InteropServices.Marshal.SizeOf<WaveInCapabilities>();
                for (var i = 0; i < deviceCount; i++)
                {
                    if (WaveInterop.waveInGetDevCaps((IntPtr)i, out var caps, capsSize) == NAudio.MmResult.NoError)
                    {
                        devices.Add(new AudioDeviceInfo(i, caps.ProductName));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating input devices: {ex.Message}");
            }
            
            return devices;
        }
    }
}
