using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// Windows-based playback engine using NAudio WaveOutEvent.
    /// </summary>
    public sealed class WindowsPlaybackEngine : IPlaybackEngine
    {
        private WaveOutEvent? _waveOut;
        private ISampleProvider? _currentProvider;
        private bool _disposed;

        public PlaybackState PlaybackState => _waveOut?.PlaybackState ?? PlaybackState.Stopped;

        public float Volume
        {
            get => _waveOut?.Volume ?? 0f;
            set
            {
                if (_waveOut != null)
                {
                    _waveOut.Volume = Math.Clamp(value, 0f, 1f);
                }
            }
        }

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public void Init(ISampleProvider sampleProvider)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowsPlaybackEngine));

            _currentProvider = sampleProvider ?? throw new ArgumentNullException(nameof(sampleProvider));

            if (_waveOut == null)
            {
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, e);
            }

            _waveOut.Init(sampleProvider);
        }

        public void Play()
        {
            if (_waveOut != null)
            {
                _waveOut.Play();
            }
        }

        public void Pause()
        {
            if (_waveOut != null)
            {
                try
                {
                    _waveOut.Pause();
                }
                catch
                {
                    // Swallow pause exceptions
                }
            }
        }

        public void Stop()
        {
            if (_waveOut != null)
            {
                try
                {
                    _waveOut.Stop();
                }
                catch
                {
                    // Swallow stop exceptions
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_waveOut != null)
            {
                try
                {
                    _waveOut.Dispose();
                }
                catch
                {
                }
            }

            _disposed = true;
        }
    }
}
