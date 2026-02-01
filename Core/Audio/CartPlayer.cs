using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenBroadcaster.Core.Diagnostics;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class CartPlayer : IDisposable
    {
        private readonly List<CartInstance> _active = new();
        private readonly object _sync = new();
        private int _deviceNumber;
        private float _volume = 1f;
        private WaveFormat? _encoderTapFormat;
        private AudioSampleBlockHandler? _encoderTapHandler;

        public event EventHandler<float>? LevelChanged;

        public void SelectOutputDevice(int deviceNumber)
        {
            _deviceNumber = deviceNumber;
        }

        public void SetEncoderTap(WaveFormat? targetFormat, AudioSampleBlockHandler? handler)
        {
            lock (_sync)
            {
                _encoderTapFormat = targetFormat;
                _encoderTapHandler = handler;
            }
        }

        public CartPlayback Play(string filePath, bool loop = false, Action<TimeSpan>? elapsedCallback = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A valid audio path is required", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Cart audio source not found", filePath);
            }

            var instance = new CartInstance(filePath, _deviceNumber, loop, _volume, elapsedCallback, OnInstanceCompleted, OnLevelChanged, _encoderTapFormat, _encoderTapHandler);
            var playback = new CartPlayback(instance.Stop);
            instance.AttachPlayback(playback);
            lock (_sync)
            {
                _active.Add(instance);
            }

            instance.Play();
            return playback;
        }

        public float SetVolume(float volume)
        {
            var applied = Math.Clamp(volume, 0f, 1f);
            CartInstance[] snapshot;

            lock (_sync)
            {
                _volume = applied;
                snapshot = _active.ToArray();
            }

            foreach (var instance in snapshot)
            {
                instance.SetVolume(applied);
            }

            return _volume;
        }

        private void OnInstanceCompleted(CartInstance instance)
        {
            lock (_sync)
            {
                _active.Remove(instance);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                foreach (var instance in _active.ToArray())
                {
                    instance.Dispose();
                }

                _active.Clear();
            }
        }

        private void OnLevelChanged(float level)
        {
            LevelChanged?.Invoke(this, level);
        }

        private sealed class CartInstance : IDisposable
        {
            private readonly AudioFileReader _reader;
            private readonly bool _loopEnabled;
            private readonly SampleChannel _sampleChannel;
            private MeteringSampleProvider _meteringProvider = null!;
            private readonly WaveOutEvent _waveOut;
            private readonly Timer _timer;
            private readonly Action<CartInstance> _completed;
            private readonly Action<TimeSpan>? _elapsedCallback;
            private readonly Action<float> _levelChanged;
            private bool _isDisposed;
            private bool _stopRequested;
            private CartPlayback? _playback;

            public CartInstance(string filePath, int deviceNumber, bool loopEnabled, float initialVolume, Action<TimeSpan>? elapsedCallback, Action<CartInstance> completed, Action<float> levelChanged, WaveFormat? tapFormat, AudioSampleBlockHandler? tapHandler)
            {
                if (!PlatformDetection.SupportsWindowsAudio)
                {
                    throw new PlatformNotSupportedException(
                        $"Audio playback is only supported on Windows. Running on: {PlatformDetection.ArchitectureInfo}");
                }

                _reader = new AudioFileReader(filePath);
                _loopEnabled = loopEnabled;
                _sampleChannel = new SampleChannel(_reader, true);
                _sampleChannel.Volume = initialVolume;
                _sampleChannel.PreVolumeMeter += SampleChannelOnPreVolumeMeter;
                var playbackSource = BuildPlaybackSource(_sampleChannel, tapFormat, tapHandler);
                _waveOut = new WaveOutEvent { DeviceNumber = deviceNumber };
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(playbackSource);
                _waveOut.Volume = initialVolume;
                _elapsedCallback = elapsedCallback;
                _completed = completed;
                _levelChanged = levelChanged;

                _timer = new Timer(150);
                _timer.Elapsed += OnTimerElapsed;
            }

            public void Play()
            {
                if (_elapsedCallback != null)
                {
                    _timer.Start();
                }

                _waveOut.Play();
            }

            public void Stop()
            {
                if (_isDisposed)
                {
                    return;
                }

                _stopRequested = true;
                _timer.Stop();
                _waveOut.Stop();
            }

            public void AttachPlayback(CartPlayback playback)
            {
                _playback = playback;
            }

            public void SetVolume(float volume)
            {
                if (_isDisposed)
                {
                    return;
                }

                var applied = Math.Clamp(volume, 0f, 1f);
                _sampleChannel.Volume = applied;
                _waveOut.Volume = applied;
            }

            private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
            {
                _elapsedCallback?.Invoke(_reader.CurrentTime);
            }

            private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
            {
                if (_loopEnabled && !_stopRequested)
                {
                    _reader.Position = 0;
                    _waveOut.Play();
                    if (_elapsedCallback != null)
                    {
                        _timer.Start();
                    }

                    return;
                }

                _timer.Stop();
                Dispose();
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Dispose();
                _meteringProvider.StreamVolume -= MeteringProviderOnStreamVolume;
                _sampleChannel.PreVolumeMeter -= SampleChannelOnPreVolumeMeter;
                _reader.Dispose();
                _completed(this);
                _playback?.NotifyCompleted();
                _playback = null;
            }

            private void MeteringProviderOnStreamVolume(object? sender, StreamVolumeEventArgs e)
            {
                if (e.MaxSampleValues.Length == 0)
                {
                    return;
                }

                _levelChanged(Math.Abs(e.MaxSampleValues[0]));
            }

            private void SampleChannelOnPreVolumeMeter(object? sender, StreamVolumeEventArgs e)
            {
                // Required for SampleChannel metering enabling.
            }

            private MeteringSampleProvider BuildPlaybackSource(ISampleProvider source, WaveFormat? tapFormat, AudioSampleBlockHandler? tapHandler)
            {
                ISampleProvider provider = source;
                if (tapHandler != null && tapFormat != null)
                {
                    provider = EnsureChannelCount(provider, tapFormat.Channels);
                    provider = EnsureSampleRate(provider, tapFormat.SampleRate);
                    provider = new TapSampleProvider(provider, tapHandler);
                }

                _meteringProvider = new MeteringSampleProvider(provider);
                _meteringProvider.StreamVolume += MeteringProviderOnStreamVolume;
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
        }
    }
}
