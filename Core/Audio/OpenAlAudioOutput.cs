using System;
using System.Buffers;
using System.Threading;
using NAudio.Wave;
using OpenTK.Audio.OpenAL;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class OpenAlAudioOutput : IAudioOutput
    {
        private const int BufferCount = 4;
        private const int BufferMilliseconds = 40;
        private readonly int _deviceNumber;
        private ALDevice _device;
        private ALContext _context;
        private int _source;
        private int[]? _buffers;
        private ISampleProvider? _provider;
        private Thread? _thread;
        private volatile bool _stopRequested;
        private volatile bool _disposed;
        private readonly object _sync = new();
        private float _volume = 1f;
        private PlaybackState _state = PlaybackState.Stopped;

        public OpenAlAudioOutput(int deviceNumber)
        {
            _deviceNumber = deviceNumber;
        }

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public PlaybackState PlaybackState => _state;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_source != 0)
                {
                    AL.Source(_source, ALSourcef.Gain, _volume);
                }
            }
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
                if (_thread == null)
                {
                    _thread = new Thread(PlaybackLoop) { IsBackground = true, Name = "OpenAL Playback" };
                    _thread.Start();
                }
                else if (_source != 0)
                {
                    AL.SourcePlay(_source);
                    _state = PlaybackState.Playing;
                }
            }
        }

        public void Pause()
        {
            if (_state != PlaybackState.Playing)
            {
                return;
            }

            AL.SourcePause(_source);
            _state = PlaybackState.Paused;
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
                AL.SourceStop(_source);
                _state = PlaybackState.Stopped;
            }

            _thread?.Join();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
            TearDown();
        }

        private void PlaybackLoop()
        {
            byte[]? byteBuffer = null;
            try
            {
                // Try to initialize OpenAL on background thread with timeout
                if (!TryEnsureContextWithTimeout(5000))
                {
                    // OpenAL initialization timed out or failed
                    _state = PlaybackState.Stopped;
                    RaiseStopped();
                    return;
                }

                var format = ResolveFormat();
                var waveFormat = _provider!.WaveFormat;
                var samplesPerBuffer = (waveFormat.SampleRate * waveFormat.Channels * BufferMilliseconds) / 1000;
                samplesPerBuffer = Math.Max(samplesPerBuffer, 256 * waveFormat.Channels);

                var floatBuffer = new float[samplesPerBuffer];
                byteBuffer = ArrayPool<byte>.Shared.Rent(samplesPerBuffer * sizeof(short));

                _buffers ??= AL.GenBuffers(BufferCount);
                for (int i = 0; i < _buffers.Length; i++)
                {
                    var bytes = FillBuffer(floatBuffer, byteBuffer, waveFormat, samplesPerBuffer);
                    if (bytes == 0)
                    {
                        RaiseStopped();
                        return;
                    }

                    unsafe
                    {
                        fixed (byte* ptr = byteBuffer)
                        {
                            AL.BufferData(_buffers[i], format, (IntPtr)ptr, bytes, waveFormat.SampleRate);
                        }
                    }
                }

                AL.SourceQueueBuffers(_source, _buffers);
                AL.Source(_source, ALSourcef.Gain, _volume);
                AL.SourcePlay(_source);
                _state = PlaybackState.Playing;

                while (!_stopRequested)
                {
                    AL.GetSource(_source, ALGetSourcei.BuffersProcessed, out var processed);
                    while (processed-- > 0)
                    {
                        var buffer = AL.SourceUnqueueBuffer(_source);
                        var bytes = FillBuffer(floatBuffer, byteBuffer, waveFormat, samplesPerBuffer);
                        if (bytes == 0)
                        {
                            AL.SourceStop(_source);
                            _state = PlaybackState.Stopped;
                            RaiseStopped();
                            return;
                        }

                        unsafe
                        {
                            fixed (byte* ptr = byteBuffer)
                            {
                                AL.BufferData(buffer, format, (IntPtr)ptr, bytes, waveFormat.SampleRate);
                            }
                        }
                        AL.SourceQueueBuffer(_source, buffer);
                    }

                    Thread.Sleep(BufferMilliseconds / 2);
                }
            }
            catch
            {
                _state = PlaybackState.Stopped;
                RaiseStopped();
            }
            finally
            {
                if (byteBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(byteBuffer);
                }
            }
        }

        private int FillBuffer(float[] floatBuffer, byte[] byteBuffer, WaveFormat waveFormat, int samplesPerBuffer)
        {
            var samplesRead = _provider!.Read(floatBuffer, 0, samplesPerBuffer);
            if (samplesRead == 0)
            {
                return 0;
            }

            var sampleCount = samplesRead;
            var byteCount = sampleCount * sizeof(short);
            var span = byteBuffer.AsSpan(0, byteCount);
            for (int i = 0; i < sampleCount; i++)
            {
                var sample = (short)Math.Clamp(floatBuffer[i] * short.MaxValue, short.MinValue, short.MaxValue);
                var offset = i * 2;
                span[offset] = (byte)(sample & 0xFF);
                span[offset + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return byteCount;
        }

        private ALFormat ResolveFormat()
        {
            var channels = _provider!.WaveFormat.Channels;
            return channels switch
            {
                1 => ALFormat.Mono16,
                2 => ALFormat.Stereo16,
                _ => throw new NotSupportedException($"Unsupported channel count {channels} for OpenAL output.")
            };
        }

        private void EnsureContext()
        {
            if (_source != 0)
            {
                return;
            }

            var deviceName = OpenAlDeviceLookup.ResolvePlaybackDeviceName(_deviceNumber);
            _device = ALC.OpenDevice(deviceName);
            if (_device == ALDevice.Null)
            {
                throw new InvalidOperationException("Unable to open OpenAL device.");
            }

            unsafe
            {
                _context = ALC.CreateContext(_device, (int*)null);
            }
            ALC.MakeContextCurrent(_context);
            _source = AL.GenSource();
        }

        private bool TryEnsureContextWithTimeout(int timeoutMs)
        {
            // Run EnsureContext on a separate thread with timeout to prevent hanging
            bool success = false;
            Exception? error = null;

            var initThread = new Thread(() =>
            {
                try
                {
                    EnsureContext();
                    success = true;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            })
            {
                IsBackground = true,
                Name = "OpenAL Init"
            };

            initThread.Start();
            if (initThread.Join(timeoutMs))
            {
                // Thread completed within timeout
                return success;
            }

            // Timeout - OpenAL initialization is hanging
            System.Diagnostics.Debug.WriteLine("[OpenAL] Initialization timeout - likely headless system with no audio devices");
            return false;
        }

        private void TearDown()
        {
            if (_source != 0)
            {
                AL.SourceStop(_source);
                AL.DeleteSource(_source);
                _source = 0;
            }

            if (_buffers != null)
            {
                AL.DeleteBuffers(_buffers);
                _buffers = null;
            }

            if (_context != ALContext.Null)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
                _context = ALContext.Null;
            }

            if (_device != ALDevice.Null)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null;
            }
        }

        private void RaiseStopped()
        {
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(null));
        }
    }
}
