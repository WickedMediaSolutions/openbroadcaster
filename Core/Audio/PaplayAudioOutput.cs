using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class PaplayAudioOutput : IAudioOutput
    {
        private ISampleProvider? _provider;
        private PlaybackState _state = PlaybackState.Stopped;
        private Thread? _playbackThread;
        private Process? _audioProcess;
        private volatile bool _stopRequested;
        private volatile bool _disposed;
        private readonly object _sync = new();
        private float _volume = 1f;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState => _state;
        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public void Init(ISampleProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public void Play()
        {
            if (_provider == null)
                throw new InvalidOperationException("Output not initialized.");

            lock (_sync)
            {
                if (_state == PlaybackState.Playing)
                    return;

                _stopRequested = false;
                _state = PlaybackState.Playing;

                if (_playbackThread == null || !_playbackThread.IsAlive)
                {
                    _playbackThread = new Thread(PlaybackLoop)
                    {
                        IsBackground = true,
                        Name = "Linux Audio Playback",
                        Priority = ThreadPriority.AboveNormal
                    };
                    _playbackThread.Start();
                }
            }
        }

        public void Pause()
        {
            lock (_sync)
            {
                if (_state == PlaybackState.Playing)
                    _state = PlaybackState.Paused;
            }
        }

        public void Stop()
        {
            Thread? threadToJoin;
            lock (_sync)
            {
                if (_state == PlaybackState.Stopped)
                    return;

                _stopRequested = true;
                _state = PlaybackState.Stopped;
                threadToJoin = _playbackThread;
                
                try 
                { 
                    if (_audioProcess != null && !_audioProcess.HasExited)
                        _audioProcess.Kill(); 
                } 
                catch { }
            }
            
            // Join outside lock to prevent deadlock
            threadToJoin?.Join(2000);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private void PlaybackLoop()
        {
            Process? process = null;
            Stream? stdin = null;
            try
            {
                var waveFormat = _provider!.WaveFormat;
                int channels = waveFormat.Channels;
                int sampleRate = waveFormat.SampleRate;

                var psi = new ProcessStartInfo
                {
                    FileName = "ffplay",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("s16le");
                psi.ArgumentList.Add("-ar");
                psi.ArgumentList.Add(sampleRate.ToString());
                psi.ArgumentList.Add("-ac");
                psi.ArgumentList.Add(channels.ToString());
                psi.ArgumentList.Add("-nodisp");
                psi.ArgumentList.Add("-autoexit");
                psi.ArgumentList.Add("-loglevel");
                psi.ArgumentList.Add("quiet");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add("pipe:0");

                process = Process.Start(psi);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start audio process");
                }

                _audioProcess = process;
                stdin = process.StandardInput.BaseStream;
                
                // Use larger buffers to reduce syscalls and prevent buffer underruns
                const int bufferSamples = 8192;
                var floatBuffer = new float[bufferSamples];
                var byteBuffer = new byte[bufferSamples * sizeof(short)];

                while (!_stopRequested && !process.HasExited)
                {
                    if (_state == PlaybackState.Paused)
                    {
                        Thread.Sleep(20);
                        continue;
                    }

                    if (_state != PlaybackState.Playing)
                        break;

                    int samplesRead;
                    try
                    {
                        samplesRead = _provider.Read(floatBuffer, 0, floatBuffer.Length);
                    }
                    catch
                    {
                        break;
                    }

                    if (samplesRead == 0)
                    {
                        // End of stream - wait briefly for ffplay to finish consuming buffer
                        Thread.Sleep(100);
                        break;
                    }

                    int byteCount = ConvertToS16LE(floatBuffer, byteBuffer, samplesRead);

                    try
                    {
                        stdin.Write(byteBuffer, 0, byteCount);
                    }
                    catch
                    {
                        break;
                    }
                }

                // Graceful shutdown - close stdin to signal EOF to ffplay
                try { stdin?.Close(); } catch { }
                stdin = null;
                
                // Wait briefly for ffplay to finish
                if (process != null && !process.HasExited)
                {
                    process.WaitForExit(500);
                }

                _state = PlaybackState.Stopped;
                PlaybackStopped?.Invoke(this, new StoppedEventArgs(null));
            }
            catch (Exception ex)
            {
                _state = PlaybackState.Stopped;
                PlaybackStopped?.Invoke(this, new StoppedEventArgs(ex));
            }
            finally
            {
                try { stdin?.Close(); } catch { }
                try 
                { 
                    if (process != null && !process.HasExited)
                        process.Kill(); 
                } 
                catch { }
                try { process?.Dispose(); } catch { }
                
                lock (_sync)
                {
                    _audioProcess = null;
                    _playbackThread = null;
                }
            }
        }

        private int ConvertToS16LE(float[] floatSamples, byte[] byteBuffer, int sampleCount)
        {
            float vol = _volume;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = floatSamples[i] * vol;
                short s16 = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
                int idx = i * 2;
                byteBuffer[idx] = (byte)(s16 & 0xFF);
                byteBuffer[idx + 1] = (byte)((s16 >> 8) & 0xFF);
            }
            return sampleCount * 2;
        }
    }
}
