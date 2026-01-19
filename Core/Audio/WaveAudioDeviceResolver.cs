using System.Collections.Generic;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class WaveAudioDeviceResolver : IAudioDeviceResolver
    {
        public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                devices.Add(new AudioDeviceInfo(i, caps.ProductName));
            }

            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            for (var i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                devices.Add(new AudioDeviceInfo(i, caps.ProductName));
            }

            return devices;
        }
    }
}
