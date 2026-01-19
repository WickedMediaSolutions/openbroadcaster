using System;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public interface IAudioEncoderTap
    {
        WaveFormat TargetFormat { get; }
        AudioSampleBlockHandler CreateSourceTap(AudioSourceType source);
        void SubmitMicrophoneSamples(WaveFormat format, ReadOnlySpan<float> samples);
    }
}
