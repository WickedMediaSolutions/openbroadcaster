using System;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public static class AudioFileReaderFactory
    {
        public static WaveStream OpenRead(string filePath)
        {
            if (OperatingSystem.IsWindows())
            {
                return new AudioFileReader(filePath);
            }

            return new FfmpegWaveStream(filePath);
        }
    }
}
