using System;
using System.Buffers;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Services
{
    public sealed class MicSampleBlockEventArgs : EventArgs, IDisposable
    {
        private readonly bool _pooled;

        public MicSampleBlockEventArgs(WaveFormat format, float[] buffer, int sampleCount, bool pooled)
        {
            Format = format;
            Buffer = buffer;
            SampleCount = sampleCount;
            _pooled = pooled;
        }

        public WaveFormat Format { get; }
        public float[] Buffer { get; }
        public int SampleCount { get; }

        public ReadOnlySpan<float> GetSamplesSpan()
        {
            return new ReadOnlySpan<float>(Buffer, 0, SampleCount);
        }

        public void Dispose()
        {
            if (_pooled)
            {
                ArrayPool<float>.Shared.Return(Buffer);
            }
        }
    }
}
