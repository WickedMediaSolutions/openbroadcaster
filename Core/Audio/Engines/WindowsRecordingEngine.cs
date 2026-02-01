using System;
using System.Buffers;
using NAudio.Wave;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Core.Audio.Engines
{
    /// <summary>
    /// Windows-based recording engine using NAudio WaveInEvent.
    /// </summary>
    public sealed class WindowsRecordingEngine : IRecordingEngine
    {
        private WaveInEvent? _waveIn;
        private readonly WaveFormat _captureFormat = new WaveFormat(44100, 1);
        private readonly WaveFormat _floatFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        private float _volume = 1.0f;
        private bool _disposed;

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public event EventHandler<MicSampleBlockEventArgs>? SamplesAvailable;
        public event EventHandler<float>? LevelChanged;

        public void StartRecording(int deviceNumber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowsRecordingEngine));

            if (deviceNumber < 0)
            {
                StopRecording();
                return;
            }

            // If already recording from the same device, do nothing
            if (_waveIn != null && _waveIn.DeviceNumber == deviceNumber)
            {
                return;
            }

            StopRecording();

            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = _captureFormat
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }

        public void StopRecording()
        {
            if (_waveIn == null)
                return;

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
            LevelChanged?.Invoke(this, 0);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0)
                return;

            // Calculate peak level for VU metering
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                if (i + 1 >= e.Buffer.Length)
                    break;

                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                var normalized = Math.Abs(sample / 32768f) * _volume;
                if (normalized > max)
                    max = normalized;
            }

            LevelChanged?.Invoke(this, max);

            // Convert samples for encoder if needed
            var sampleCount = e.BytesRecorded / 2;
            if (sampleCount <= 0 || SamplesAvailable == null)
                return;

            // Convert PCM to floating-point
            var floatBuffer = ArrayPool<float>.Shared.Rent(sampleCount);
            try
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short pcmSample = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8));
                    floatBuffer[i] = (pcmSample / 32768f) * _volume;
                }

                SamplesAvailable?.Invoke(this, new MicSampleBlockEventArgs(_floatFormat, floatBuffer, sampleCount, true));
            }
            finally
            {
                ArrayPool<float>.Shared.Return(floatBuffer);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopRecording();
            _disposed = true;
        }
    }
}
