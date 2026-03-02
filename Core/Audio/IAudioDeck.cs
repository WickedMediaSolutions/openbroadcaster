using System;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Common interface for audio playback decks across all platforms.
    /// Implemented by WindowsAudioDeck (NAudio) and LinuxAudioDeck (PulseAudio).
    /// </summary>
    public interface IAudioDeck : IDisposable
    {
        /// <summary>
        /// Gets the deck identifier (A or B).
        /// </summary>
        Models.DeckIdentifier DeckId { get; }

        /// <summary>
        /// Gets the current playback elapsed time.
        /// </summary>
        TimeSpan ElapsedTime { get; }

        /// <summary>
        /// Gets the current volume level (0.0 - 1.0).
        /// </summary>
        float Volume { get; }

        /// <summary>
        /// Event raised periodically with the current elapsed time (typically every 200ms).
        /// </summary>
        event Action<TimeSpan>? Elapsed;

        /// <summary>
        /// Event raised when playback stops (end of file reached).
        /// </summary>
        event Action? PlaybackStopped;

        /// <summary>
        /// Event raised when the audio level changes (for VU metering).
        /// Parameter is the peak level (0.0 - 1.0).
        /// </summary>
        event Action<float>? LevelChanged;

        /// <summary>
        /// Configures an encoder tap for sending audio samples to an encoder.
        /// </summary>
        void SetEncoderTap(NAudio.Wave.WaveFormat? targetFormat, AudioSampleBlockHandler? callback);

        /// <summary>
        /// Cue (load) an audio file without starting playback.
        /// </summary>
        void Cue(string filePath);

        /// <summary>
        /// Start playback of a loaded file, or cue and play a new file.
        /// </summary>
        void Play(string? filePath = null);

        /// <summary>
        /// Pause playback (can resume from same position).
        /// </summary>
        void Pause();

        /// <summary>
        /// Stop playback and reset to the beginning.
        /// </summary>
        void Stop();

        /// <summary>
        /// Select an output device for playback (0 = default).
        /// </summary>
        void SelectOutputDevice(int deviceNumber);

        /// <summary>
        /// Set the playback volume.
        /// </summary>
        /// <param name="volume">Volume level from 0.0 (silent) to 1.0 (full).</param>
        /// <returns>The applied volume level (clamped to 0.0-1.0).</returns>
        float SetVolume(float volume);
    }
}
