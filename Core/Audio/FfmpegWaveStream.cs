using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class FfmpegWaveStream : WaveStream
    {
        private readonly string _filePath;
        private readonly WaveFormat _waveFormat;
        private readonly long _length;
        private Process? _process;
        private Stream? _stdout;
        private long _position;
        private bool _disposed;

        public FfmpegWaveStream(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A valid audio path is required", nameof(filePath));
            }

            _filePath = filePath;
            var probe = FfmpegProbe.TryProbe(filePath);
            var sampleRate = probe.SampleRate > 0 ? probe.SampleRate : 44100;
            var channels = probe.Channels > 0 ? probe.Channels : 2;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

            if (probe.DurationSeconds > 0)
            {
                _length = (long)Math.Round(probe.DurationSeconds * _waveFormat.AverageBytesPerSecond);
            }

            StartProcess();
        }

        public override WaveFormat WaveFormat => _waveFormat;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value == 0)
                {
                    RestartProcess();
                    return;
                }

                throw new NotSupportedException("Seeking is not supported by the FFmpeg reader.");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FfmpegWaveStream));
            }

            if (_stdout == null)
            {
                return 0;
            }

            var read = _stdout.Read(buffer, offset, count);
            if (read > 0)
            {
                _position += read;
                return read;
            }

            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (disposing)
            {
                StopProcess();
            }

            base.Dispose(disposing);
        }

        private void RestartProcess()
        {
            _position = 0;
            StopProcess();
            StartProcess();
        }

        private void StartProcess()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("-hide_banner");
            process.StartInfo.ArgumentList.Add("-loglevel");
            process.StartInfo.ArgumentList.Add("error");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(_filePath);
            process.StartInfo.ArgumentList.Add("-f");
            process.StartInfo.ArgumentList.Add("f32le");
            process.StartInfo.ArgumentList.Add("-acodec");
            process.StartInfo.ArgumentList.Add("pcm_f32le");
            process.StartInfo.ArgumentList.Add("-ac");
            process.StartInfo.ArgumentList.Add(_waveFormat.Channels.ToString(CultureInfo.InvariantCulture));
            process.StartInfo.ArgumentList.Add("-ar");
            process.StartInfo.ArgumentList.Add(_waveFormat.SampleRate.ToString(CultureInfo.InvariantCulture));
            process.StartInfo.ArgumentList.Add("-");

            process.Start();
            process.BeginErrorReadLine();
            _process = process;
            _stdout = process.StandardOutput.BaseStream;
        }

        private void StopProcess()
        {
            try
            {
                _stdout?.Dispose();
            }
            catch
            {
            }

            _stdout = null;

            if (_process == null)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            try
            {
                _process.Dispose();
            }
            catch
            {
            }

            _process = null;
        }

        private static class FfmpegProbe
        {
            public static ProbeResult TryProbe(string filePath)
            {
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffprobe",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.StartInfo.ArgumentList.Add("-v");
                    process.StartInfo.ArgumentList.Add("error");
                    process.StartInfo.ArgumentList.Add("-select_streams");
                    process.StartInfo.ArgumentList.Add("a:0");
                    process.StartInfo.ArgumentList.Add("-show_entries");
                    process.StartInfo.ArgumentList.Add("stream=sample_rate,channels");
                    process.StartInfo.ArgumentList.Add("-show_entries");
                    process.StartInfo.ArgumentList.Add("format=duration");
                    process.StartInfo.ArgumentList.Add("-of");
                    process.StartInfo.ArgumentList.Add("json");
                    process.StartInfo.ArgumentList.Add(filePath);

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return default;
                    }

                    var json = JsonDocument.Parse(output);
                    var root = json.RootElement;
                    var sampleRate = 0;
                    var channels = 0;
                    var duration = 0d;

                    if (root.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0)
                    {
                        var stream = streams[0];
                        if (stream.TryGetProperty("sample_rate", out var sampleRateElement))
                        {
                            int.TryParse(sampleRateElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sampleRate);
                        }

                        if (stream.TryGetProperty("channels", out var channelsElement))
                        {
                            channels = channelsElement.GetInt32();
                        }
                    }

                    if (root.TryGetProperty("format", out var formatElement)
                        && formatElement.TryGetProperty("duration", out var durationElement))
                    {
                        double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
                    }

                    return new ProbeResult(sampleRate, channels, duration);
                }
                catch
                {
                    return default;
                }
            }

            public readonly struct ProbeResult
            {
                public ProbeResult(int sampleRate, int channels, double durationSeconds)
                {
                    SampleRate = sampleRate;
                    Channels = channels;
                    DurationSeconds = durationSeconds;
                }

                public int SampleRate { get; }
                public int Channels { get; }
                public double DurationSeconds { get; }
            }
        }
    }
}
