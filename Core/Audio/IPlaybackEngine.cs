using System;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Cross-platform abstraction for audio playback.
    /// Implementations support Windows, Linux (PulseAudio, ALSA, Jack), and macOS.
    /// </summary>
    public interface IPlaybackEngine : IDisposable
    {
        /// <summary>
        /// Gets the audio playback state (Playing, Paused, Stopped).
        /// </summary>
        PlaybackState PlaybackState { get; }

        /// <summary>
        /// Gets or sets the playback volume (0.0 to 1.0).
        /// </summary>
        float Volume { get; set; }

        /// <summary>
        /// Initializes the playback engine with an audio sample provider.
        /// </summary>
        /// <param name="sampleProvider">The source audio data provider</param>
        void Init(ISampleProvider sampleProvider);

        /// <summary>
        /// Starts playback of the initialized audio.
        /// </summary>
        void Play();

        /// <summary>
        /// Pauses playback without resetting position.
        /// </summary>
        void Pause();

        /// <summary>
        /// Stops playback and resets the device state.
        /// </summary>
        void Stop();

        /// <summary>
        /// Raised when playback stops (either naturally or due to an error).
        /// </summary>
        event EventHandler<StoppedEventArgs>? PlaybackStopped;
    }
}
