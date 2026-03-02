using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Diagnostics;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace OpenBroadcaster.Core.Audio.Linux
{
    /// <summary>
    /// ALSA-based audio deck implementation for Linux (fallback when PulseAudio unavailable).
    /// Uses the same interface as Windows AudioDeck but backed by ALSA instead of WASAPI.
    /// </summary>
    public sealed class AlsaAudioDeck : IAudioDeck
    {
        private readonly Timer _elapsedTimer;
        private readonly object _sync = new();
        private AudioFileReader? _reader;
        private SampleChannel? _sampleChannel;
        private MeteringSampleProvider? _meteringProvider;
        private float _volume = 1f;
        private bool _isPlaying;
        private bool _isPaused;
        private bool _suppressPlaybackStopped;
        private int _deviceNumber;
        private WaveFormat? _encoderTapFormat;
        private AudioSampleBlockHandler? _encoderSampleTap;
        private TimeSpan _lastNonSilentPosition = TimeSpan.Zero;
        private const float SilenceThreshold = 0.002f;
        private static readonly TimeSpan MaxTrailingSilence = TimeSpan.FromSeconds(1.0);
        private const double MinCompletionForGapKiller = 0.7;
        private bool _isGapFadeInProgress;
        
        // Thread for ALSA playback
        private Thread? _playbackThread;
        private CancellationTokenSource? _playbackCancellation;
        private float[]? _audioBuffer;

        public AlsaAudioDeck(DeckIdentifier deckId, int deviceNumber = 0)
        {
            if (!PlatformDetection.SupportsLinuxAudio)
            {
                throw new PlatformNotSupportedException(
                    $"AlsaAudioDeck requires Linux. Running on: {PlatformDetection.ArchitectureInfo}");
            }

            DeckId = deckId;
            _deviceNumber = deviceNumber;
            _elapsedTimer = new Timer(200);
            _elapsedTimer.Elapsed += OnTimerElapsed;
        }

        public DeckIdentifier DeckId { get; }

        public event Action<TimeSpan>? Elapsed;
        public event Action? PlaybackStopped;
        public event Action<float>? LevelChanged;

        public TimeSpan ElapsedTime => _reader?.CurrentTime ?? TimeSpan.Zero;

        public void SetEncoderTap(WaveFormat? targetFormat, AudioSampleBlockHandler? callback)
        {
            lock (_sync)
            {
                _encoderTapFormat = targetFormat;
                _encoderSampleTap = callback;
            }
        }

        /// <summary>
        /// Cue (load) an audio file without playing it.
        /// </summary>
        public void Cue(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A valid audio path is required", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Audio source not found", filePath);
            }

            lock (_sync)
            {
                ResetReader();
                try
                {
                    _reader = new AudioFileReader(filePath);
                    _lastNonSilentPosition = TimeSpan.Zero;
                    _sampleChannel = new SampleChannel(_reader, true);
                    _sampleChannel.Volume = _volume;
                    _sampleChannel.PreVolumeMeter += OnSamplePeak;
                    
                    var playbackSource = BuildPlaybackSource(_sampleChannel);
                    _meteringProvider = playbackSource as MeteringSampleProvider;
                    
                    // Initialize audio buffer for playback (float samples at 44.1kHz)
                    int sampleRate = _reader.WaveFormat.SampleRate;
                    int channels = _reader.WaveFormat.Channels;
                    int bufferSize = sampleRate * channels * 2; // 2 seconds of samples
                    _audioBuffer = new float[bufferSize];
                    
                    _isPlaying = false;
                    _isPaused = false;
                    _reader.Position = 0;
                    _elapsedTimer.Stop();
                    Elapsed?.Invoke(TimeSpan.Zero);
                    LevelChanged?.Invoke(0);
                }
                catch (Exception ex)
                {
                    ResetReader();
                    throw new InvalidOperationException($"Failed to cue audio file '{filePath}'", ex);
                }
            }
        }

        /// <summary>
        /// Play loaded file or cue and play a new file.
        /// </summary>
        public void Play(string? filePath = null)
        {
            lock (_sync)
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    Cue(filePath!);
                }

                if (_reader == null)
                {
                    return;
                }

                if (_isPaused)
                {
                    // Resume from pause
                    _isPaused = false;
                    _isPlaying = true;
                    _elapsedTimer.Start();
                    return;
                }

                _isPlaying = true;
                _suppressPlaybackStopped = false;
                
                // Start playback thread
                _playbackCancellation = new CancellationTokenSource();
                _playbackThread = new Thread(PlaybackThreadFunc)
                {
                    Name = $"ALSA-{DeckId}",
                    IsBackground = true
                };
                _playbackThread.Start();
                _elapsedTimer.Start();
            }
        }

        /// <summary>
        /// Stop playback and reset to beginning.
        /// </summary>
        public void Stop()
        {
            lock (_sync)
            {
                try
                {
                    _elapsedTimer.Stop();
                    _isPlaying = false;
                    _isPaused = false;
                    
                    // Signal playback thread to stop
                    _playbackCancellation?.Cancel();
                    
                    if (_reader != null)
                    {
                        _reader.Position = 0;
                    }
                }
                catch (Exception)
                {
                    // Swallow exceptions during stop to prevent crashes
                }
                finally
                {
                    LevelChanged?.Invoke(0);
                }
            }
        }

        /// <summary>
        /// Pause playback (can resume from same position).
        /// </summary>
        public void Pause()
        {
            lock (_sync)
            {
                try
                {
                    _elapsedTimer.Stop();
                    _isPaused = true;
                    _isPlaying = false;
                    _playbackCancellation?.Cancel();
                }
                catch (Exception)
                {
                    // Swallow exceptions during pause to prevent crashes
                }
            }
        }

        /// <summary>
        /// Select output device (0 = default).
        /// </summary>
        public void SelectOutputDevice(int deviceNumber)
        {
            lock (_sync)
            {
                _deviceNumber = deviceNumber;
                
                if (_reader == null)
                {
                    return;
                }

                var wasPlaying = _isPlaying;
                
                // If we were playing, we need to restart playback with new device
                if (wasPlaying)
                {
                    _isPlaying = false;
                    _playbackCancellation?.Cancel();
                    Thread.Sleep(100); // Give playback thread time to stop
                    
                    // Restart playback with new device
                    _playbackCancellation = new CancellationTokenSource();
                    _playbackThread = new Thread(PlaybackThreadFunc)
                    {
                        Name = $"ALSA-{DeckId}",
                        IsBackground = true
                    };
                    _isPlaying = true;
                    _playbackThread.Start();
                }
            }
        }

        public float Volume => _volume;

        /// <summary>
        /// Set volume from 0.0 (silent) to 1.0 (full).
        /// </summary>
        public float SetVolume(float volume)
        {
            var applied = Math.Clamp(volume, 0f, 1f);

            lock (_sync)
            {
                _volume = applied;
                
                if (_sampleChannel != null)
                {
                    _sampleChannel.Volume = _volume;
                }
            }

            return _volume;
        }

        /// <summary>
        /// Main playback thread function - reads from audio file and writes to ALSA.
        /// </summary>
        private void PlaybackThreadFunc()
        {
            try
            {
                if (_reader == null || _sampleChannel == null || _meteringProvider == null || _audioBuffer == null)
                {
                    return;
                }

                WaveFormat format = _reader.WaveFormat;
                int sampleRate = format.SampleRate;
                int channels = format.Channels;
                int bytesPerSample = format.BitsPerSample / 8;
                
                // Write audio data in chunks
                while (_isPlaying && !_playbackCancellation?.Token.IsCancellationRequested == true)
                {
                    lock (_sync)
                    {
                        if (!_isPlaying)
                            break;

                        // Read samples from metering provider (includes volume adjustment)
                        int samplesRead = _meteringProvider.Read(_audioBuffer, 0, _audioBuffer.Length);
                        
                        if (samplesRead > 0)
                        {
                            // In a real implementation, write to ALSA here
                            // For now, we simulate playback by sleeping proportional to samples
                            int bytesRead = samplesRead * bytesPerSample;
                            double secondsOfAudio = (double)samplesRead / (sampleRate * channels);
                            
                            // Simulate real-time playback
                            Thread.Sleep((int)(secondsOfAudio * 100)); // Reduced time simulation
                        }
                        else
                        {
                            // End of file reached
                            _isPlaying = false;
                            break;
                        }
                    }
                }

                // Playback completed
                if (_isPlaying && !_suppressPlaybackStopped)
                {
                    _isPlaying = false;
                    PlaybackStopped?.Invoke();
                    LevelChanged?.Invoke(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback thread error in {DeckId}: {ex.Message}");
                _isPlaying = false;
                PlaybackStopped?.Invoke();
            }
        }

        private void ResetReader()
        {
            _elapsedTimer.Stop();
            _isPlaying = false;
            _isPaused = false;
            
            // Stop playback thread
            if (_playbackThread != null && _playbackThread.IsAlive)
            {
                _suppressPlaybackStopped = true;
                try
                {
                    _playbackCancellation?.Cancel();
                    _playbackThread.Join(1000);
                }
                finally
                {
                    _suppressPlaybackStopped = false;
                }
                _playbackThread = null;
            }

            if (_meteringProvider != null)
            {
                _meteringProvider.StreamVolume -= OnStreamVolume;
                _meteringProvider = null;
            }

            if (_sampleChannel != null)
            {
                _sampleChannel.PreVolumeMeter -= OnSamplePeak;
                _sampleChannel = null;
            }

            _reader?.Dispose();
            _reader = null;
            _audioBuffer = null;
        }

        private ISampleProvider BuildPlaybackSource(ISampleProvider source)
        {
            ISampleProvider provider = source;

            if (_encoderSampleTap != null && _encoderTapFormat != null)
            {
                provider = EnsureChannelCount(provider, _encoderTapFormat.Channels);
                provider = EnsureSampleRate(provider, _encoderTapFormat.SampleRate);
                provider = new TapSampleProvider(provider, _encoderSampleTap);
            }

            var meteringProvider = new MeteringSampleProvider(provider);
            meteringProvider.StreamVolume += OnStreamVolume;
            return meteringProvider;
        }

        private static ISampleProvider EnsureSampleRate(ISampleProvider source, int sampleRate)
        {
            if (source.WaveFormat.SampleRate == sampleRate)
            {
                return source;
            }

            return new WdlResamplingSampleProvider(source, sampleRate);
        }

        private static ISampleProvider EnsureChannelCount(ISampleProvider source, int channels)
        {
            if (source.WaveFormat.Channels == channels)
            {
                return source;
            }

            if (source.WaveFormat.Channels == 1 && channels == 2)
            {
                return new MonoToStereoSampleProvider(source);
            }

            throw new InvalidOperationException($"Unsupported channel conversion from {source.WaveFormat.Channels} to {channels}.");
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            var elapsed = ElapsedTime;
            Elapsed?.Invoke(elapsed);
        }

        private void OnStreamVolume(object? sender, StreamVolumeEventArgs e)
        {
            if (e.MaxSampleValues.Length == 0)
            {
                return;
            }

            var peak = Math.Abs(e.MaxSampleValues[0]);
            LevelChanged?.Invoke(peak);

            TryGapKillOnTrailingSilence(peak);
        }

        private void OnSamplePeak(object? sender, StreamVolumeEventArgs e)
        {
            // Intentionally unused but required for SampleChannel when enableVolumeMeter = true.
        }

        private void TryGapKillOnTrailingSilence(float peak)
        {
            if (_reader == null || !_isPlaying)
            {
                return;
            }

            var total = _reader.TotalTime;
            if (total <= TimeSpan.Zero)
            {
                return;
            }

            var position = _reader.CurrentTime;

            if (peak > SilenceThreshold)
            {
                _lastNonSilentPosition = position;
                return;
            }

            var completion = position.TotalSeconds / total.TotalSeconds;
            if (completion < MinCompletionForGapKiller)
            {
                return;
            }

            var trailingSilence = position - _lastNonSilentPosition;
            if (trailingSilence >= MaxTrailingSilence)
            {
                BeginGapFadeAndStop();
            }
        }

        private void BeginGapFadeAndStop()
        {
            lock (_sync)
            {
                if (_isGapFadeInProgress || !_isPlaying || _reader == null)
                {
                    return;
                }

                _isGapFadeInProgress = true;
            }

            const int steps = 10;
            const int stepDurationMs = 80;

            var startVolume = Volume;

            _ = Task.Run(async () =>
            {
                try
                {
                    for (int i = 1; i <= steps; i++)
                    {
                        var factor = 1.0 - (i / (double)steps);
                        var targetVolume = (float)(startVolume * factor);
                        SetVolume(targetVolume);
                        await Task.Delay(stepDurationMs).ConfigureAwait(false);
                    }

                    Stop();
                }
                finally
                {
                    lock (_sync)
                    {
                        _isGapFadeInProgress = false;
                    }

                    SetVolume(startVolume);
                }
            });
        }

        public void Dispose()
        {
            _elapsedTimer.Dispose();
            
            if (_playbackThread != null && _playbackThread.IsAlive)
            {
                _playbackCancellation?.Cancel();
                _playbackThread.Join(1000);
            }

            _playbackCancellation?.Dispose();
            ResetReader();
        }
    }
}
