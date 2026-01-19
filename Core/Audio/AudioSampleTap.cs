using System;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Delegate invoked when a source produces a block of samples that should be duplicated to another bus.
    /// </summary>
    /// <param name="format">Wave format describing the source buffer.</param>
    /// <param name="samples">Span containing interleaved floating-point samples.</param>
    public delegate void AudioSampleBlockHandler(WaveFormat format, ReadOnlySpan<float> samples);
}
