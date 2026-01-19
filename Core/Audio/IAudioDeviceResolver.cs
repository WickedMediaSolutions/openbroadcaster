using System.Collections.Generic;

namespace OpenBroadcaster.Core.Audio
{
    public interface IAudioDeviceResolver
    {
        IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
        IReadOnlyList<AudioDeviceInfo> GetInputDevices();
    }
}
