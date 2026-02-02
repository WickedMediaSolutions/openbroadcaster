using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// PulseAudio-based microphone capture using ffmpeg for Linux.
    /// Uses ffmpeg's pulse input for better compatibility across Linux audio configurations.
    /// </summary>
    internal sealed class PulseAudioMicCapture : IDisposable
    {
        private static readonly ILogger<PulseAudioMicCapture> Logger = AppLogger.CreateLogger<PulseAudioMicCapture>();
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bufferSamples;
        private Process? _process;
        private Thread? _thread;
        private Thread? _stderrThread;
        private volatile bool _running;
        private int _capturedBytes;

        public PulseAudioMicCapture(int sampleRate, int channels, int bufferMilliseconds = 50)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bufferSamples = Math.Max(1024, (sampleRate * channels * bufferMilliseconds) / 1000);
        }

        public event EventHandler<PulseCaptureEventArgs>? SamplesCaptured;
        
        /// <summary>
        /// Gets whether the capture is currently receiving audio data.
        /// </summary>
        public bool IsCapturing => _capturedBytes > 0;

        public void Start(string? deviceName)
        {
            Stop();
            _capturedBytes = 0;

            var actualDevice = string.IsNullOrWhiteSpace(deviceName) ? "default" : deviceName;
            Logger.LogInformation("Starting mic capture from device: {Device}, SampleRate: {Rate}, Channels: {Channels}", 
                actualDevice, _sampleRate, _channels);

            // Try ALSA first if available (better compatibility on ChromeOS Crostini)
            if (TryStartWithAlsa(actualDevice))
            {
                return;
            }

            // Fall back to PulseAudio
            StartWithPulseAudio(actualDevice);
        }

        private bool TryStartWithAlsa(string deviceName)
        {
            // On ChromeOS Crostini, ALSA devices may be available even if PulseAudio doesn't expose them
            if (!System.IO.File.Exists("/dev/snd/pcmC0D0c"))
            {
                return false; // No ALSA devices available
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Input from ALSA device directly
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("alsa");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add("hw:0,0"); // Default ALSA capture device
                
                // Output format: signed 16-bit little-endian PCM
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("s16le");
                psi.ArgumentList.Add("-ar");
                psi.ArgumentList.Add(_sampleRate.ToString());
                psi.ArgumentList.Add("-ac");
                psi.ArgumentList.Add(_channels.ToString());
                psi.ArgumentList.Add("pipe:1");

                Logger.LogDebug("Trying ALSA capture with: ffmpeg -f alsa -i hw:0,0 -f s16le -ar {Rate} -ac {Channels} pipe:1", 
                    _sampleRate, _channels);

                _process = Process.Start(psi);
                if (_process == null)
                {
                    Logger.LogWarning("Failed to start ffmpeg with ALSA, will try PulseAudio");
                    return false;
                }

                Logger.LogInformation("Started ALSA mic capture, PID: {PID}", _process.Id);

                _running = true;
                
                // Start stderr drain thread
                _stderrThread = new Thread(DrainStderr) { IsBackground = true, Name = "ffmpeg stderr drain" };
                _stderrThread.Start();
                
                _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "ALSA Mic Capture" };
                _thread.Start();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ALSA capture failed, will try PulseAudio");
                return false;
            }
        }

        private void StartWithPulseAudio(string deviceName)
        {
            // Use ffmpeg for capture - more reliable than parec across different audio configurations
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Capture stderr to avoid blocking
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Input from PulseAudio
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("pulse");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(deviceName);
            
            // Output format: signed 16-bit little-endian PCM
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("s16le");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add(_sampleRate.ToString());
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add(_channels.ToString());
            psi.ArgumentList.Add("pipe:1"); // Output to stdout explicitly

            Logger.LogDebug("Trying PulseAudio capture with ffmpeg -f pulse -i {Device}", deviceName);

            _process = Process.Start(psi);
            if (_process == null)
            {
                Logger.LogError("Failed to start ffmpeg process for mic capture");

                throw new InvalidOperationException("Failed to start ffmpeg process for microphone capture.");
            }

            Logger.LogInformation("ffmpeg mic capture process started, PID: {PID}", _process.Id);

            _running = true;
            
            // Start stderr drain thread to prevent blocking
            _stderrThread = new Thread(DrainStderr) { IsBackground = true, Name = "ffmpeg stderr drain" };
            _stderrThread.Start();
            
            _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "PulseAudio Mic Capture" };
            _thread.Start();
        }
        
        private void DrainStderr()
        {
            if (_process == null) return;
            try
            {
                // Just read and discard stderr to prevent buffer blocking
                while (_running && !_process.HasExited)
                {
                    _process.StandardError.ReadLine();
                }
            }
            catch { }
        }

        public void Stop()
        {
            _running = false;
            Logger.LogInformation("Stopping mic capture, captured {Bytes} bytes total", _capturedBytes);
            
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch { }

            _thread?.Join(1000);
            _thread = null;
            
            _stderrThread?.Join(500);
            _stderrThread = null;

            try { _process?.Dispose(); } catch { }
            _process = null;
        }

        private void CaptureLoop()
        {
            if (_process == null) return;

            var stdout = _process.StandardOutput.BaseStream;
            var bytesPerSample = 2; // 16-bit
            var bufferSize = _bufferSamples * _channels * bytesPerSample;
            var byteBuffer = new byte[bufferSize];
            var loopCount = 0;

            Logger.LogDebug("Mic capture loop started, buffer size: {BufferSize}", bufferSize);

            while (_running && !_process.HasExited)
            {
                try
                {
                    var bytesRead = stdout.Read(byteBuffer, 0, bufferSize);
                    if (bytesRead <= 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    loopCount++;
                    if (loopCount == 1 || loopCount % 100 == 0)
                    {
                        Logger.LogDebug("Mic capture: read {Bytes} bytes, total {Total}, loops {Loops}", 
                            bytesRead, _capturedBytes, loopCount);
                    }

                    _capturedBytes += bytesRead;
                    var sampleCount = bytesRead / bytesPerSample;
                    var shortBuffer = ArrayPool<short>.Shared.Rent(sampleCount);

                    for (int i = 0; i < sampleCount && (i * 2 + 1) < bytesRead; i++)
                    {
                        shortBuffer[i] = (short)(byteBuffer[i * 2] | (byteBuffer[i * 2 + 1] << 8));
                    }

                    SamplesCaptured?.Invoke(this, new PulseCaptureEventArgs(shortBuffer, sampleCount, true));
                }
                catch (IOException)
                {
                    // Process terminated
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class PulseCaptureEventArgs : EventArgs, IDisposable
    {
        private readonly bool _pooled;

        public PulseCaptureEventArgs(short[] buffer, int sampleCount, bool pooled)
        {
            Buffer = buffer;
            SampleCount = sampleCount;
            _pooled = pooled;
        }

        public short[] Buffer { get; }
        public int SampleCount { get; }

        public void Dispose()
        {
            if (_pooled)
            {
                ArrayPool<short>.Shared.Return(Buffer);
            }
        }
    }
}
