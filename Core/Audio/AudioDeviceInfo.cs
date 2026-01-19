namespace OpenBroadcaster.Core.Audio
{
    public sealed class AudioDeviceInfo
    {
        public AudioDeviceInfo(int deviceNumber, string productName)
        {
            DeviceNumber = deviceNumber;
            ProductName = productName;
        }

        public int DeviceNumber { get; }
        public string ProductName { get; }
    }
}
