using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenBroadcaster.Core.Audio;

namespace OpenBroadcaster.Core.Streaming
{
    public sealed class SharedEncoderAudioSource : IEncoderAudioSource, IAudioEncoderTap, IDisposable
    {
        private const int FrameRateHz = 25; // ~40 ms frames
        private readonly AudioRoutingGraph _routingGraph;
        private readonly WaveFormat _encoderFormat;
        private readonly WaveFormat _mixFormat;
        private readonly MixingSampleProvider _mixingProvider;
        private readonly Dictionary<AudioSourceType, QueuedSampleProvider> _inputs = new();
        private readonly object _gate = new();
        private readonly int _frameSampleCount;
        private readonly float[] _mixBuffer;
        private readonly byte[] _pcmBuffer;
        private CancellationTokenSource? _cts;
        private Task? _pumpTask;
        private volatile bool _isRunning;
        private bool _disposed;

        public SharedEncoderAudioSource(AudioRoutingGraph routingGraph, int sampleRate = 44100, int channels = 2)
        {
            _routingGraph = routingGraph ?? throw new ArgumentNullException(nameof(routingGraph));
            _encoderFormat = new WaveFormat(sampleRate, 16, channels);
            _mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            _mixingProvider = new MixingSampleProvider(_mixFormat)
            {
                ReadFully = true
            };

            foreach (var source in Enum.GetValues(typeof(AudioSourceType)).Cast<AudioSourceType>())
            {
                var provider = new QueuedSampleProvider(_mixFormat);
                _inputs[source] = provider;
                _mixingProvider.AddMixerInput(provider);
            }

            _frameSampleCount = (sampleRate / FrameRateHz) * channels;
            _mixBuffer = new float[_frameSampleCount];
            _pcmBuffer = new byte[_frameSampleCount * sizeof(short)];
        }

        public WaveFormat Format => _encoderFormat;
        public WaveFormat TargetFormat => _mixFormat;

        public event EventHandler<EncoderAudioFrameEventArgs>? FrameReady;

        public void Start()
        {
            lock (_gate)
            {
                if (_pumpTask != null)
                {
                    return;
                }

                _cts = new CancellationTokenSource();
                _pumpTask = Task.Run(() => PumpAsync(_cts.Token), CancellationToken.None);
                _isRunning = true;
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            Task? worker;

            lock (_gate)
            {
                if (_pumpTask == null)
                {
                    return;
                }

                cts = _cts;
                worker = _pumpTask;
                _cts = null;
                _pumpTask = null;
            }

            if (cts != null)
            {
                cts.Cancel();
            }

            if (worker != null)
            {
                try
                {
                    worker.Wait();
                }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
                {
                }
            }

            foreach (var provider in _inputs.Values)
            {
                provider.Reset();
            }

            _isRunning = false;
            cts?.Dispose();
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

        public AudioSampleBlockHandler CreateSourceTap(AudioSourceType source)
        {
            return (format, samples) => SubmitSamples(source, format, samples);
        }

        public void SubmitMicrophoneSamples(WaveFormat format, ReadOnlySpan<float> samples)
        {
            SubmitSamples(AudioSourceType.Microphone, format, samples);
        }

        private void SubmitSamples(AudioSourceType source, WaveFormat format, ReadOnlySpan<float> samples)
        {
            if (samples.IsEmpty)
            {
                return;
            }

            if (!_isRunning)
            {
                return;
            }

            var routes = _routingGraph.GetRoute(source);
            if (routes.Count == 0 || !routes.Contains(AudioBus.Encoder))
            {
                return;
            }

            if (!_inputs.TryGetValue(source, out var provider))
            {
                return;
            }

            provider.Enqueue(format, samples, _mixFormat);
        }

        private async Task PumpAsync(CancellationToken cancellationToken)
        {
            var frameDelay = TimeSpan.FromMilliseconds((_frameSampleCount / _mixFormat.Channels) * 1000.0 / _mixFormat.SampleRate);

            while (!cancellationToken.IsCancellationRequested)
            {
                _mixingProvider.Read(_mixBuffer, 0, _mixBuffer.Length);
                var bytes = ConvertToPcm16(_mixBuffer, _mixBuffer.Length, _pcmBuffer);
                var rented = ArrayPool<byte>.Shared.Rent(bytes);
                Buffer.BlockCopy(_pcmBuffer, 0, rented, 0, bytes);
                FrameReady?.Invoke(this, new EncoderAudioFrameEventArgs(rented, bytes, true));
                try
                {
                    await Task.Delay(frameDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static int ConvertToPcm16(float[] source, int samples, byte[] destination)
        {
            var bytesWritten = samples * sizeof(short);
            for (int i = 0; i < samples; i++)
            {
                var value = Math.Clamp(source[i], -1f, 1f);
                var sample = (short)(value * short.MaxValue);
                var offset = i * 2;
                destination[offset] = (byte)(sample & 0xFF);
                destination[offset + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return bytesWritten;
        }

        private sealed class QueuedSampleProvider : ISampleProvider
        {
            private readonly WaveFormat _format;
            private readonly Queue<SampleBuffer> _buffers = new();
            private SampleBuffer? _active;
            private readonly object _sync = new();

            public QueuedSampleProvider(WaveFormat format)
            {
                _format = format;
            }

            public WaveFormat WaveFormat => _format;

            public void Enqueue(WaveFormat sourceFormat, ReadOnlySpan<float> samples, WaveFormat targetFormat)
            {
                if (samples.IsEmpty)
                {
                    return;
                }

                if (sourceFormat.SampleRate != targetFormat.SampleRate || sourceFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                {
                    throw new InvalidOperationException("Encoder tap provided incompatible sample format.");
                }

                if (sourceFormat.Channels == targetFormat.Channels)
                {
                    CopySamples(samples);
                    return;
                }

                if (sourceFormat.Channels == 1 && targetFormat.Channels == 2)
                {
                    DuplicateMonoToStereo(samples);
                    return;
                }

                throw new InvalidOperationException($"Unsupported channel conversion ({sourceFormat.Channels} -> {targetFormat.Channels}).");
            }

            private void CopySamples(ReadOnlySpan<float> samples)
            {
                var buffer = ArrayPool<float>.Shared.Rent(samples.Length);
                samples.CopyTo(buffer);
                lock (_sync)
                {
                    _buffers.Enqueue(new SampleBuffer(buffer, samples.Length));
                }
            }

            private void DuplicateMonoToStereo(ReadOnlySpan<float> samples)
            {
                var frames = samples.Length;
                var buffer = ArrayPool<float>.Shared.Rent(frames * 2);
                for (int i = 0; i < frames; i++)
                {
                    var value = samples[i];
                    var offset = i * 2;
                    buffer[offset] = value;
                    buffer[offset + 1] = value;
                }

                lock (_sync)
                {
                    _buffers.Enqueue(new SampleBuffer(buffer, frames * 2));
                }
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var written = 0;
                lock (_sync)
                {
                    while (written < count)
                    {
                        if (_active == null)
                        {
                            if (_buffers.Count == 0)
                            {
                                break;
                            }

                            _active = _buffers.Dequeue();
                        }

                        var available = _active.Length - _active.Offset;
                        var toCopy = Math.Min(available, count - written);
                        Array.Copy(_active.Buffer, _active.Offset, buffer, offset + written, toCopy);
                        written += toCopy;
                        _active.Offset += toCopy;
                        if (_active.Offset >= _active.Length)
                        {
                            ArrayPool<float>.Shared.Return(_active.Buffer);
                            _active = null;
                        }
                    }
                }

                if (written < count)
                {
                    Array.Clear(buffer, offset + written, count - written);
                    written = count;
                }

                return written;
            }

            public void Reset()
            {
                lock (_sync)
                {
                    if (_active != null)
                    {
                        ArrayPool<float>.Shared.Return(_active.Buffer);
                        _active = null;
                    }

                    while (_buffers.Count > 0)
                    {
                        var item = _buffers.Dequeue();
                        ArrayPool<float>.Shared.Return(item.Buffer);
                    }
                }
            }

            private sealed class SampleBuffer
            {
                public SampleBuffer(float[] buffer, int length)
                {
                    Buffer = buffer;
                    Length = length;
                    Offset = 0;
                }

                public float[] Buffer { get; }
                public int Length { get; }
                public int Offset { get; set; }
            }
        }
    }
}
