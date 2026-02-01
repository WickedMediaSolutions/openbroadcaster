using System;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class WaveOutAudioOutput : IAudioOutput
    {
        private readonly WaveOutEvent _waveOut;

        public WaveOutAudioOutput(int deviceNumber)
        {
            _waveOut = new WaveOutEvent { DeviceNumber = deviceNumber };
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public PlaybackState PlaybackState => _waveOut.PlaybackState;

        public float Volume
        {
            get => _waveOut.Volume;
            set => _waveOut.Volume = value;
        }

        public void Init(ISampleProvider provider)
        {
            _waveOut.Init(provider);
        }

        public void Play()
        {
            _waveOut.Play();
        }

        public void Pause()
        {
            _waveOut.Pause();
        }

        public void Stop()
        {
            _waveOut.Stop();
        }

        public void Dispose()
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            PlaybackStopped?.Invoke(this, e);
        }
    }
}
