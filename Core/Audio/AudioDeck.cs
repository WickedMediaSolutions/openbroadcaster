using System;
using System.IO;
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
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _reader;
        private SampleChannel? _sampleChannel;
        private MeteringSampleProvider? _meteringProvider;
        private int _deviceNumber;
        private float _volume = 1f;
        private WaveFormat? _encoderTapFormat;
        private AudioSampleBlockHandler? _encoderSampleTap;

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
                _reader = new AudioFileReader(filePath);
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

        public void SelectOutputDevice(int deviceNumber)
        {
            lock (_sync)
            {
                _deviceNumber = deviceNumber;
                if (_reader == null)
                {
                    RecreateOutput(null);
                    return;
                }

                var wasPlaying = _waveOut?.PlaybackState == PlaybackState.Playing;
                RecreateOutput(_reader);
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
                _waveOut = new WaveOutEvent { DeviceNumber = _deviceNumber };
                _waveOut.Volume = _volume;
                _waveOut.PlaybackStopped += OnPlaybackStopped;
            }
        }

        private void RecreateOutput(AudioFileReader? reader)
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (reader != null)
            {
                InitializeOutput();
                _waveOut!.Init(reader);
                _waveOut!.Volume = _volume;
            }
        }

        private void ResetReader()
        {
            _elapsedTimer.Stop();
            if (_waveOut != null)
            {
                _waveOut.Stop();
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
    }
}
