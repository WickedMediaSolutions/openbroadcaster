using System.Collections.Generic;
using OpenTK.Audio.OpenAL;

namespace OpenBroadcaster.Core.Audio
{
    public static class OpenAlDeviceLookup
    {
        public static string? ResolvePlaybackDeviceName(int deviceNumber)
        {
            var devices = GetPlaybackDevices();
            if (deviceNumber >= 0 && deviceNumber < devices.Count)
            {
                return devices[deviceNumber];
            }

            return null;
        }

        public static string? ResolveCaptureDeviceName(int deviceNumber)
        {
            var devices = GetCaptureDevices();
            if (deviceNumber >= 0 && deviceNumber < devices.Count)
            {
                return devices[deviceNumber];
            }

            return null;
        }

        public static IReadOnlyList<string> GetPlaybackDevices()
        {
            try
            {
                var devices = new List<string>();
                var devicePtr = ALC.GetString(ALDevice.Null, AlcGetString.AllDevicesSpecifier);
                if (!string.IsNullOrEmpty(devicePtr))
                {
                    devices.Add(devicePtr);
                }
                return devices;
            }
            catch
            {
                return new List<string>();
            }
        }

        public static IReadOnlyList<string> GetCaptureDevices()
        {
            try
            {
                var devices = new List<string>();
                var devicePtr = ALC.GetString(ALDevice.Null, AlcGetString.CaptureDeviceSpecifier);
                if (!string.IsNullOrEmpty(devicePtr))
                {
                    devices.Add(devicePtr);
                }
                return devices;
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
