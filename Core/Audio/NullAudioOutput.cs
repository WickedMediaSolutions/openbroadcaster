using System;
using System.Threading;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Null audio output that simulates playback without requiring audio hardware.
    /// Used as fallback for headless systems where OpenAL initialization fails.
    /// </summary>
    public sealed class NullAudioOutput : IAudioOutput
    {
        private ISampleProvider? _provider;
        private PlaybackState _state = PlaybackState.Stopped;
        private Thread? _simulationThread;
        private volatile bool _stopRequested;
        private volatile bool _disposed;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public PlaybackState PlaybackState => _state;

        public float Volume { get; set; } = 1f;

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

            if (_state == PlaybackState.Playing)
            {
                return;
            }

            _stopRequested = false;
            _state = PlaybackState.Playing;

            if (_simulationThread == null)
            {
                _simulationThread = new Thread(SimulatePlayback)
                {
                    IsBackground = true,
                    Name = "Null Audio Simulation"
                };
                _simulationThread.Start();
            }
        }

        public void Pause()
        {
            if (_state != PlaybackState.Playing)
            {
                return;
            }

            _state = PlaybackState.Paused;
        }

        public void Stop()
        {
            if (_state == PlaybackState.Stopped)
            {
                return;
            }

            _stopRequested = true;
            _state = PlaybackState.Stopped;
            _simulationThread?.Join(1000);
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

        private void SimulatePlayback()
        {
            try
            {
                var floatBuffer = new float[4096];
                var waveFormat = _provider!.WaveFormat;
                var bytesPerMs = waveFormat.AverageBytesPerSecond / 1000;

                while (!_stopRequested)
                {
                    var samplesRead = _provider.Read(floatBuffer, 0, floatBuffer.Length);
                    if (samplesRead == 0)
                    {
                        break;
                    }

                    // Simulate playback by sleeping proportional to samples
                    var byteCount = samplesRead * 2; // assuming 16-bit
                    var sleepMs = (int)(byteCount / (double)bytesPerMs);
                    if (sleepMs > 0)
                    {
                        Thread.Sleep(Math.Min(sleepMs, 50));
                    }
                }

                _state = PlaybackState.Stopped;
                PlaybackStopped?.Invoke(this, new StoppedEventArgs(null));
            }
            catch (Exception ex)
            {
                _state = PlaybackState.Stopped;
                PlaybackStopped?.Invoke(this, new StoppedEventArgs(ex));
            }
        }
    }
}
