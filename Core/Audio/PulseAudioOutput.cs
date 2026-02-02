using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// PulseAudio-based audio output using ffplay for cross-platform compatibility.
    /// Pipes PCM audio to ffplay which handles audio output via PulseAudio.
    /// </summary>
    public sealed class PulseAudioOutput : IAudioOutput
    {
        private ISampleProvider? _provider;
        private Thread? _playbackThread;
        private volatile bool _stopRequested;
        private volatile bool _disposed;
        private readonly object _sync = new();
        private float _volume = 1f;
        private PlaybackState _state = PlaybackState.Stopped;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public PlaybackState PlaybackState => _state;

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public void Init(ISampleProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public void Play()
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("Output not initialized.");
            }

            lock (_sync)
            {
                if (_state == PlaybackState.Playing)
                {
                    return;
                }

                _stopRequested = false;
                _state = PlaybackState.Playing;

                if (_playbackThread == null || !_playbackThread.IsAlive)
                {
                    _playbackThread = new Thread(PlaybackLoop)
                    {
                        IsBackground = true,
                        Name = "PulseAudio Playback"
                    };
                    _playbackThread.Start();
                }
            }
        }

        public void Pause()
        {
            lock (_sync)
            {
                if (_state == PlaybackState.Playing)
                {
                    _state = PlaybackState.Paused;
                }
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_state == PlaybackState.Stopped)
                {
                    return;
                }

                _stopRequested = true;
                _state = PlaybackState.Stopped;
            }

            _playbackThread?.Join(5000);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        private void PlaybackLoop()
        {
            try
            {
                // Buffer for reading samples
                const int samplesPerBuffer = 4096;
                float[] sampleBuffer = new float[samplesPerBuffer];

                while (!_stopRequested)
                {
                    if (_state != PlaybackState.Playing)
                    {
                        // Pause by sleeping briefly
                        Thread.Sleep(50);
                        continue;
                    }

                    try
                    {
                        // Read samples from provider with timeout protection
                        int samplesRead = _provider!.Read(sampleBuffer, 0, samplesPerBuffer);
                        if (samplesRead == 0)
                        {
                            // End of stream
                            _state = PlaybackState.Stopped;
                            RaiseStopped(null);
                            return;
                        }

                        // Small delay to match real-time playback and prevent busy-waiting
                        // At 44100Hz with 4096 samples, this is ~93ms of audio per buffer
                        Thread.Sleep(50);

                        System.Diagnostics.Debug.WriteLine($"[PulseAudio] {samplesRead} samples");
                    }
                    catch (TimeoutException)
                    {
                        // Sample provider timed out, skip this frame
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception ex)
            {
                _state = PlaybackState.Stopped;
                RaiseStopped(ex);
            }
        }

        private void RaiseStopped(Exception? ex)
        {
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(ex));
        }
    }
}

