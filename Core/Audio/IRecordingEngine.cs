using System;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Cross-platform abstraction for audio recording/input.
    /// Implementations support Windows, Linux (PulseAudio, ALSA, Jack), and macOS.
    /// </summary>
    public interface IRecordingEngine : IDisposable
    {
        /// <summary>
        /// Gets or sets the recording volume (0.0 to 1.0).
        /// </summary>
        float Volume { get; set; }

        /// <summary>
        /// Starts recording from the specified audio device.
        /// </summary>
        /// <param name="deviceNumber">The device index to record from</param>
        void StartRecording(int deviceNumber);

        /// <summary>
        /// Stops recording and cleans up resources.
        /// </summary>
        void StopRecording();

        /// <summary>
        /// Raised when audio samples are available for processing.
        /// </summary>
        event EventHandler<MicSampleBlockEventArgs>? SamplesAvailable;

        /// <summary>
        /// Raised when the recording level changes (for VU metering).
        /// </summary>
        event EventHandler<float>? LevelChanged;
    }
}
