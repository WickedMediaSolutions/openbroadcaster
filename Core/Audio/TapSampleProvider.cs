using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenBroadcaster.Core.Audio
{
    internal sealed class TapSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly AudioSampleBlockHandler _callback;
        private readonly WaveFormat _format;

        public TapSampleProvider(ISampleProvider source, AudioSampleBlockHandler callback)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _format = source.WaveFormat;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var read = _source.Read(buffer, offset, count);
            if (read > 0)
            {
                _callback(_format, new ReadOnlySpan<float>(buffer, offset, read));
            }

            return read;
        }
    }
}
