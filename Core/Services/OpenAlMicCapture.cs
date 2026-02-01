using System;
using System.Buffers;
using System.Threading;
using OpenTK.Audio.OpenAL;

namespace OpenBroadcaster.Core.Services
{
    internal sealed class OpenAlMicCapture : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bufferSamples;
        private ALCaptureDevice _device;
        private Thread? _thread;
        private volatile bool _running;

        public OpenAlMicCapture(int sampleRate, int channels, int bufferMilliseconds = 50)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bufferSamples = Math.Max(1024 * channels, (sampleRate * channels * bufferMilliseconds) / 1000);
        }

        public event EventHandler<OpenAlCaptureEventArgs>? SamplesCaptured;

        public void Start(string? deviceName)
        {
            Stop();
            _device = ALC.CaptureOpenDevice(deviceName, (uint)_sampleRate, ALFormat.Mono16, _bufferSamples);
            if (_device == ALCaptureDevice.Null)
            {
                throw new InvalidOperationException("Unable to open OpenAL capture device.");
            }

            ALC.CaptureStart(_device);
            _running = true;
            _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "OpenAL Capture" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            _thread?.Join();
            _thread = null;

            if (_device != ALCaptureDevice.Null)
            {
                ALC.CaptureStop(_device);
                ALC.CaptureCloseDevice(_device);
                _device = ALCaptureDevice.Null;
            }
        }

        private void CaptureLoop()
        {
            while (_running)
            {
                ALC.GetInteger(_device, AlcGetInteger.CaptureSamples, 1, out int availableSamples);
                if (availableSamples <= 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                var samplesToRead = Math.Min(availableSamples, _bufferSamples);
                var buffer = ArrayPool<short>.Shared.Rent(samplesToRead * _channels);
                unsafe
                {
                    fixed (short* ptr = buffer)
                    {
                        ALC.CaptureSamples(_device, (IntPtr)ptr, samplesToRead);
                    }
                }
                SamplesCaptured?.Invoke(this, new OpenAlCaptureEventArgs(buffer, samplesToRead * _channels, true));
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class OpenAlCaptureEventArgs : EventArgs, IDisposable
    {
        private readonly bool _pooled;

        public OpenAlCaptureEventArgs(short[] buffer, int sampleCount, bool pooled)
        {
            Buffer = buffer;
            SampleCount = sampleCount;
            _pooled = pooled;
        }

        public short[] Buffer { get; }
        public int SampleCount { get; }

        public void Dispose()
        {
            if (_pooled)
            {
                ArrayPool<short>.Shared.Return(Buffer);
            }
        }
    }
}
