using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// Windows-based device enumerator using NAudio WaveOut/WaveIn APIs.
    /// </summary>
    public sealed class WindowsAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        public string BackendName => "Windows WASAPI (NAudio)";

        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

#if WINDOWS
            try
            {
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    devices.Add(new AudioDeviceInfo(i, caps.ProductName));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating playback devices: {ex.Message}");
            }
#endif

            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetRecordingDevices()
        {
            var devices = new List<AudioDeviceInfo>();

#if WINDOWS
            try
            {
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    devices.Add(new AudioDeviceInfo(i, caps.ProductName));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error enumerating recording devices: {ex.Message}");
            }
#endif

            return devices;
        }
    }
}
