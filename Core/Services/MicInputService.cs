using System;
using System.Buffers;
using System.Linq;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster.Core.Services
{
    public sealed class MicInputService : IDisposable
    {
        private static readonly ILogger<MicInputService> Logger = AppLogger.CreateLogger<MicInputService>();
        private WaveInEvent? _waveIn;
        private PulseAudioMicCapture? _pulseCapture;
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
            Logger.LogInformation("MicInputService.Start called with device {DeviceNumber}", deviceNumber);
            
            // On Linux, -1 means use default device
            // On Windows, -1 means no device selected
            if (deviceNumber < 0 && OperatingSystem.IsWindows())
            {
                Logger.LogWarning("Device number is negative on Windows, stopping");
                Stop();
                return;
            }

            if (_deviceNumber == deviceNumber && (_waveIn != null || _pulseCapture != null))
            {
                Logger.LogInformation("Already capturing from device {DeviceNumber}", deviceNumber);
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

            // On Linux, use PulseAudio capture via ffmpeg
            var deviceName = ResolvePulseInputDevice(deviceNumber);
            Logger.LogInformation("Resolved device {DeviceNumber} to PulseAudio source: {DeviceName}", deviceNumber, deviceName ?? "default");
            
            _pulseCapture = new PulseAudioMicCapture(_captureFormat.SampleRate, _captureFormat.Channels);
            _pulseCapture.SamplesCaptured += OnPulseSamplesCaptured;
            _pulseCapture.Start(deviceName);
            Logger.LogInformation("PulseAudioMicCapture started");
        }

        private static string? ResolvePulseInputDevice(int deviceNumber)
        {
            // Get the list of PulseAudio sources and resolve by device number (PulseAudio index)
            var resolver = new LinuxAudioDeviceResolver();
            var devices = resolver.GetInputDevices();
            
            Logger.LogDebug("Looking for device {DeviceNumber} among {Count} input devices", deviceNumber, devices.Count);
            foreach (var d in devices)
            {
                Logger.LogDebug("  Device {Num}: {Name}", d.DeviceNumber, d.ProductName);
            }
            
            // Find device by its actual device number (PulseAudio index), not list position
            var device = devices.FirstOrDefault(d => d.DeviceNumber == deviceNumber);
            if (device != null)
            {
                Logger.LogDebug("Found matching device: {Name}", device.ProductName);
                return device.ProductName;
            }
            
            Logger.LogWarning("Device {DeviceNumber} not found, using default", deviceNumber);
            return null; // Use default
        }

        public void Stop()
        {
            if (_waveIn == null && _pulseCapture == null)
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

            if (_pulseCapture != null)
            {
                _pulseCapture.SamplesCaptured -= OnPulseSamplesCaptured;
                _pulseCapture.Dispose();
                _pulseCapture = null;
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

        private void OnPulseSamplesCaptured(object? sender, PulseCaptureEventArgs e)
        {
            try
            {
                if (e.SampleCount <= 0)
                {
                    return;
                }

                float max = 0;
                short maxRaw = 0;
                
                // Calculate max level first
                for (int i = 0; i < e.SampleCount; i++)
                {
                    // Safely get absolute value - cast to int first to avoid overflow
                    var rawAbsInt = Math.Abs((int)e.Buffer[i]);
                    if (rawAbsInt > Math.Abs((int)maxRaw)) maxRaw = (short)Math.Min(rawAbsInt, short.MaxValue);
                    
                    var normalized = Math.Abs((e.Buffer[i] / 32768f) * _volume);
                    if (normalized > max)
                    {
                        max = normalized;
                    }
                }

                // Log level periodically for debugging (every ~50 calls)
                if (_debugLogCounter++ % 50 == 0)
                {
                    Logger.LogDebug("Mic level: {Level:F4}, maxRaw: {MaxRaw}, samples: {Count}", max, maxRaw, e.SampleCount);
                }

                // Fire level changed event
                try
                {
                    LevelChanged?.Invoke(this, max);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in LevelChanged event handler");
                }

                // Only send samples if there's a listener
                if (SamplesAvailable != null && e.SampleCount > 0)
                {
                    try
                    {
                        var buffer = ArrayPool<float>.Shared.Rent(e.SampleCount);
                        
                        // Convert samples with bounds checking
                        for (int i = 0; i < e.SampleCount && i < buffer.Length; i++)
                        {
                            buffer[i] = (e.Buffer[i] / 32768f) * _volume;
                        }

                        SamplesAvailable?.Invoke(this, new MicSampleBlockEventArgs(_floatFormat, buffer, e.SampleCount, true));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing mic samples");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in OnPulseSamplesCaptured");
            }
            finally
            {
                try
                {
                    e.Dispose();
                }
                catch { }
            }
        }
        
        private int _debugLogCounter = 0;

        public void Dispose()
        {
            Stop();
        }
    }
}
