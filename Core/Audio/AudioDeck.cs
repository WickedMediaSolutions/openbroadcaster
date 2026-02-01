using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenBroadcaster.Core.Models;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class AudioDeck : IDisposable
    {
        private readonly Timer _elapsedTimer;
        private readonly object _sync = new();
        private IAudioOutput? _waveOut;
        private WaveStream? _reader;
        private SampleChannel? _sampleChannel;
        private MeteringSampleProvider? _meteringProvider;
        private int _deviceNumber;
        private float _volume = 1f;
        // When true, suppress raising PlaybackStopped for internal resets (cueing new tracks)
        private bool _suppressPlaybackStopped;
        private WaveFormat? _encoderTapFormat;
        private AudioSampleBlockHandler? _encoderSampleTap;
        private TimeSpan _lastNonSilentPosition = TimeSpan.Zero;
        private const float SilenceThreshold = 0.002f; // Roughly -54 dBFS
        private static readonly TimeSpan MaxTrailingSilence = TimeSpan.FromSeconds(1.0);
        private const double MinCompletionForGapKiller = 0.7; // Only engage near the end
        private bool _isGapFadeInProgress;

        public AudioDeck(DeckIdentifier deckId, int deviceNumber = 0)
        {
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
                _reader = AudioFileReaderFactory.OpenRead(filePath);
                _lastNonSilentPosition = TimeSpan.Zero;
                _sampleChannel = new SampleChannel(_reader, true);
                _sampleChannel.Volume = _volume;
                _sampleChannel.PreVolumeMeter += OnSamplePeak;
                var playbackSource = BuildPlaybackSource(_sampleChannel);
                InitializeOutput();
                _waveOut!.Init(playbackSource);
                _waveOut!.Volume = _volume;
                _reader.Position = 0;
                _elapsedTimer.Stop();
                Elapsed?.Invoke(TimeSpan.Zero);
                LevelChanged?.Invoke(0);
            }
        }

        public void Play(string? filePath = null)
        {
            lock (_sync)
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    Cue(filePath!);
                }

                if (_waveOut == null)
                {
                    return;
                }

                _waveOut.Play();
                _elapsedTimer.Start();
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                try
                {
                    _elapsedTimer.Stop();
                    _waveOut?.Stop();
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

        public void Pause()
        {
            lock (_sync)
            {
                try
                {
                    _elapsedTimer.Stop();
                    _waveOut?.Pause();
                }
                catch (Exception)
                {
                    // Swallow exceptions during pause to prevent crashes
                }
            }
        }

        public void SelectOutputDevice(int deviceNumber)
        {
            lock (_sync)
            {
                _deviceNumber = deviceNumber;
                if (_reader == null)
                {
                    RecreateOutput();
                    return;
                }

                var wasPlaying = _waveOut?.PlaybackState == PlaybackState.Playing;
                RecreateOutput();
                if (wasPlaying == true)
                {
                    _waveOut!.Play();
                    _elapsedTimer.Start();
                }
            }
        }

        private void InitializeOutput()
        {
            if (_waveOut == null)
            {
                _waveOut = AudioOutputFactory.Create(_deviceNumber);
                _waveOut.Volume = _volume;
                _waveOut.PlaybackStopped += OnPlaybackStopped;
            }
        }

        private void RecreateOutput()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_sampleChannel != null)
            {
                if (_meteringProvider != null)
                {
                    _meteringProvider.StreamVolume -= OnStreamVolume;
                    _meteringProvider = null;
                }

                InitializeOutput();
                var playbackSource = BuildPlaybackSource(_sampleChannel);
                _waveOut!.Init(playbackSource);
                _waveOut!.Volume = _volume;
            }
        }

        private void ResetReader()
        {
            _elapsedTimer.Stop();
            if (_waveOut != null)
            {
                // Stop and dispose the current output without publishing a playback-completed event
                _suppressPlaybackStopped = true;
                try
                {
                    _waveOut.Stop();
                }
                finally
                {
                    _suppressPlaybackStopped = false;
                }
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Dispose();
                _waveOut = null;
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

            _meteringProvider = new MeteringSampleProvider(provider);
            _meteringProvider.StreamVolume += OnStreamVolume;
            return _meteringProvider;
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

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            _elapsedTimer.Stop();
            if (_suppressPlaybackStopped)
            {
                return;
            }
            PlaybackStopped?.Invoke();
            LevelChanged?.Invoke(0);
        }

        public void Dispose()
        {
            _elapsedTimer.Dispose();
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Dispose();
            }

            _reader?.Dispose();
            if (_meteringProvider != null)
            {
                _meteringProvider.StreamVolume -= OnStreamVolume;
            }

            if (_sampleChannel != null)
            {
                _sampleChannel.PreVolumeMeter -= OnSamplePeak;
            }
            _meteringProvider = null;
            _sampleChannel = null;
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

        public float Volume => _volume;

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

                if (_waveOut != null)
                {
                    _waveOut.Volume = _volume;
                }
            }

            return _volume;
        }

        private void TryGapKillOnTrailingSilence(float peak)
        {
            // Only act while actively playing with a valid reader
            if (_reader == null || _waveOut == null || _waveOut.PlaybackState != PlaybackState.Playing)
            {
                return;
            }

            var total = _reader.TotalTime;
            if (total <= TimeSpan.Zero)
            {
                return;
            }

            var position = _reader.CurrentTime;

            // Update last non-silent position whenever audio is above threshold
            if (peak > SilenceThreshold)
            {
                _lastNonSilentPosition = position;
                return;
            }

            // Don't treat early quiet passages as end-of-track silence
            var completion = position.TotalSeconds / total.TotalSeconds;
            if (completion < MinCompletionForGapKiller)
            {
                return;
            }

            var trailingSilence = position - _lastNonSilentPosition;
            if (trailingSilence >= MaxTrailingSilence)
            {
                // Fade out and stop a bit early to avoid long dead air at the end.
                // This will raise PlaybackStopped and allow normal auto-advance.
                BeginGapFadeAndStop();
            }
        }

        private void BeginGapFadeAndStop()
        {
            lock (_sync)
            {
                if (_isGapFadeInProgress || _waveOut == null || _reader == null || _waveOut.PlaybackState != PlaybackState.Playing)
                {
                    return;
                }

                _isGapFadeInProgress = true;
            }

            const int steps = 10;
            const int stepDurationMs = 80; // ~0.8s total fade

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
                }
            });
        }
    }
}
