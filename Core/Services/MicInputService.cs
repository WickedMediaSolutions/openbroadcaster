using System;
using System.Buffers;
using NAudio.Wave;
using OpenBroadcaster.Core.Audio;

namespace OpenBroadcaster.Core.Services
{
    public sealed class MicInputService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private OpenAlMicCapture? _openAlCapture;
        private int _deviceNumber = -1;
        private readonly WaveFormat _captureFormat = new WaveFormat(44100, 1);
        private readonly WaveFormat _floatFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        private float _volume = 1.0f;

        public event EventHandler<float>? LevelChanged;
        public event EventHandler<MicSampleBlockEventArgs>? SamplesAvailable;

        public void SetVolume(double volume)
        {
            _volume = (float)Math.Clamp(volume, 0.0, 1.0);
        }

        public void Start(int deviceNumber)
        {
            if (deviceNumber < 0)
            {
                Stop();
                return;
            }

            if (_deviceNumber == deviceNumber && (_waveIn != null || _openAlCapture != null))
            {
                return;
            }

            Stop();

            _deviceNumber = deviceNumber;

            if (OperatingSystem.IsWindows())
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = _captureFormat
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
                return;
            }

            var deviceName = OpenAlDeviceLookup.ResolveCaptureDeviceName(deviceNumber);
            _openAlCapture = new OpenAlMicCapture(_captureFormat.SampleRate, _captureFormat.Channels);
            _openAlCapture.SamplesCaptured += OnOpenAlSamplesCaptured;
            _openAlCapture.Start(deviceName);
        }

        public void Stop()
        {
            if (_waveIn == null && _openAlCapture == null)
            {
                return;
            }

            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                try
                {
                    _waveIn.StopRecording();
                }
                catch
                {
                }

                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_openAlCapture != null)
            {
                _openAlCapture.SamplesCaptured -= OnOpenAlSamplesCaptured;
                _openAlCapture.Dispose();
                _openAlCapture = null;
            }
            _deviceNumber = -1;
            LevelChanged?.Invoke(this, 0);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0)
            {
                return;
            }

            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                if (i + 1 >= e.Buffer.Length)
                {
                    break;
                }

                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                var normalized = Math.Abs(sample / 32768f) * _volume;
                if (normalized > max)
                {
                    max = normalized;
                }
            }

            LevelChanged?.Invoke(this, max);

            var sampleCount = e.BytesRecorded / 2;
            if (sampleCount <= 0 || SamplesAvailable == null)
            {
                return;
            }

            var buffer = ArrayPool<float>.Shared.Rent(sampleCount);
            for (int index = 0, sampleIndex = 0; index + 1 < e.BytesRecorded && sampleIndex < sampleCount; index += 2, sampleIndex++)
            {
                short sample = (short)(e.Buffer[index] | (e.Buffer[index + 1] << 8));
                buffer[sampleIndex] = (sample / 32768f) * _volume;
            }

            SamplesAvailable?.Invoke(this, new MicSampleBlockEventArgs(_floatFormat, buffer, sampleCount, true));
        }

        private void OnOpenAlSamplesCaptured(object? sender, OpenAlCaptureEventArgs e)
        {
            try
            {
                if (e.SampleCount <= 0)
                {
                    return;
                }

                float max = 0;
                var buffer = ArrayPool<float>.Shared.Rent(e.SampleCount);
                for (int i = 0; i < e.SampleCount; i++)
                {
                    var normalized = (e.Buffer[i] / 32768f) * _volume;
                    buffer[i] = normalized;
                    var abs = Math.Abs(normalized);
                    if (abs > max)
                    {
                        max = abs;
                    }
                }

                LevelChanged?.Invoke(this, max);
                SamplesAvailable?.Invoke(this, new MicSampleBlockEventArgs(_floatFormat, buffer, e.SampleCount, true));
            }
            finally
            {
                e.Dispose();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
