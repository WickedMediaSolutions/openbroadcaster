using System;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public interface IAudioOutput : IDisposable
    {
        event EventHandler<StoppedEventArgs>? PlaybackStopped;
        PlaybackState PlaybackState { get; }
        float Volume { get; set; }
        void Init(ISampleProvider provider);
        void Play();
        void Pause();
        void Stop();
    }

    public static class AudioOutputFactory
    {
        public static IAudioOutput Create(int deviceNumber)
        {
            if (OperatingSystem.IsWindows())
            {
                return new WaveOutAudioOutput(deviceNumber);
            }

            if (OperatingSystem.IsLinux())
            {
                // Use paplay (PulseAudio CLI) for Linux audio output
                return new PaplayAudioOutput();
            }

            // Fallback for unknown platforms
            return new NullAudioOutput();
        }
    }
}
