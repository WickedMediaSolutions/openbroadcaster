using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using OpenBroadcaster.Core.Diagnostics;
using Timer = System.Threading.Timer;

namespace OpenBroadcaster.Core.Streaming
{
    public interface IEncoderAudioSource : IDisposable
    {
        event EventHandler<EncoderAudioFrameEventArgs>? FrameReady;
        WaveFormat Format { get; }
        void Start();
        void Stop();
    }

    public sealed class EncoderAudioFrameEventArgs : EventArgs, IDisposable
    {
        private readonly bool _pooled;

        public EncoderAudioFrameEventArgs(byte[] buffer, int bytesRecorded, bool pooled)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
            _pooled = pooled;
        }

        public byte[] Buffer { get; }
        public int BytesRecorded { get; }

        public void Dispose()
        {
            if (_pooled)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
    }

    public interface IEncoderAudioSourceFactory
    {
        IEncoderAudioSource Create(int deviceNumber);
    }

    public sealed class EncoderAudioSourceFactory : IEncoderAudioSourceFactory
    {
        public IEncoderAudioSource Create(int deviceNumber)
        {
            try
            {
                return new WasapiLoopbackAudioSource(deviceNumber);
            }
            catch
            {
                return new NullEncoderAudioSource();
            }
        }
    }

    public sealed class NullEncoderAudioSource : IEncoderAudioSource
    {
        private readonly Timer _timer;
        private bool _running;
        private readonly byte[] _silence;

        public NullEncoderAudioSource()
        {
            Format = new WaveFormat(44100, 16, 2);
            _silence = new byte[Format.AverageBytesPerSecond / 50]; // ~20ms of silence
            _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public event EventHandler<EncoderAudioFrameEventArgs>? FrameReady;
        public WaveFormat Format { get; }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _timer.Change(0, 20);
        }

        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTick(object? state)
        {
            if (!_running)
            {
                return;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(_silence.Length);
            Array.Clear(buffer, 0, _silence.Length);
            FrameReady?.Invoke(this, new EncoderAudioFrameEventArgs(buffer, _silence.Length, true));
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
        }
    }

    public sealed class WasapiLoopbackAudioSource : IEncoderAudioSource
    {
        private readonly int _deviceNumber;
        private readonly ILogger<WasapiLoopbackAudioSource> _logger;
        private WasapiLoopbackCapture? _capture;
        private bool _disposed;

        public WasapiLoopbackAudioSource(int deviceNumber)
        {
            _deviceNumber = deviceNumber;
            _logger = AppLogger.CreateLogger<WasapiLoopbackAudioSource>();
            Format = new WaveFormat(44100, 16, 2);
        }

        public event EventHandler<EncoderAudioFrameEventArgs>? FrameReady;
        public WaveFormat Format { get; private set; }

        public void Start()
        {
            if (_capture != null)
            {
                return;
            }

            var device = ResolveDevice(_deviceNumber);
            _capture = device != null ? new WasapiLoopbackCapture(device) : new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            Format = new WaveFormat(_capture.WaveFormat.SampleRate, 16, _capture.WaveFormat.Channels);
            _capture.StartRecording();
            _logger.LogInformation("Encoder audio capture started on {Device}", device?.FriendlyName ?? "default device");
        }

        public void Stop()
        {
            if (_capture == null)
            {
                return;
            }

            try
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.StopRecording();
            }
            catch
            {
                // ignore shutdown errors
            }
            finally
            {
                _capture.Dispose();
                _capture = null;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0)
            {
                return;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(e.BytesRecorded / 2 + 32);
            var source = MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded));
            var dest = MemoryMarshal.Cast<byte, short>(buffer.AsSpan());
            var count = 0;
            foreach (var sample in source)
            {
                var value = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
                dest[count++] = value;
            }

            var bytesWritten = count * sizeof(short);
            FrameReady?.Invoke(this, new EncoderAudioFrameEventArgs(buffer, bytesWritten, true));
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                _logger.LogError(e.Exception, "Encoder audio capture stopped unexpectedly");
            }
        }

        private static MMDevice? ResolveDevice(int deviceNumber)
        {
            try
            {
#if WINDOWS
                using var enumerator = new MMDeviceEnumerator();
                if (deviceNumber < 0)
                {
                    return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }

                var targetName = WaveOut.GetCapabilities(deviceNumber).ProductName;
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    if (device.FriendlyName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return device;
                    }
                }

                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
#else
                throw new PlatformNotSupportedException(
                    $"Loopback audio capture is only supported on Windows. Running on: {PlatformDetection.ArchitectureInfo}");
#endif
            }
            catch
            {
                return null;
            }
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
    }
}
