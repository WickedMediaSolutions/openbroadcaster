using System;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class AudioDeviceInfo : IEquatable<AudioDeviceInfo>
    {
        public AudioDeviceInfo(int deviceNumber, string productName)
        {
            DeviceNumber = deviceNumber;
            ProductName = productName;
        }

        public int DeviceNumber { get; }
        public string ProductName { get; }

        public bool Equals(AudioDeviceInfo? other)
        {
            if (other is null) return false;
            return DeviceNumber == other.DeviceNumber;
        }

        public override bool Equals(object? obj) => Equals(obj as AudioDeviceInfo);
        
        public override int GetHashCode() => DeviceNumber.GetHashCode();
    }
}
