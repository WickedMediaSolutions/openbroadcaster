using System.Collections.Generic;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Cross-platform abstraction for enumerating audio devices.
    /// Each platform/audio-library has a specific implementation.
    /// </summary>
    public interface IAudioDeviceEnumerator
    {
        /// <summary>
        /// Gets the name of the audio backend (Windows WASAPI, PulseAudio, ALSA, etc.)
        /// </summary>
        string BackendName { get; }

        /// <summary>
        /// Gets all available playback/output devices.
        /// </summary>
        IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();

        /// <summary>
        /// Gets all available recording/input devices.
        /// </summary>
        IReadOnlyList<AudioDeviceInfo> GetRecordingDevices();
    }
}
