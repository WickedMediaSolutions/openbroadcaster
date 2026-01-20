using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
        public sealed class AudioService : IDisposable
        {
            private bool _micEnabled = false;
            private int _lastMicDeviceNumber = -1;
            private double _micVolume = 1.0;

            /// <summary>
            /// Enables or disables the microphone input to the encoder audio.
            /// </summary>
            public void SetMicEnabled(bool enabled)
            {
                _micEnabled = enabled;
                if (enabled)
                {
                    // Route microphone to both Mic bus (for VU) and Encoder bus (for output)
                    _routingGraph.Route(AudioSourceType.Microphone, new[] { AudioBus.Mic, AudioBus.Encoder });
                    if (_lastMicDeviceNumber >= 0)
                    {
                        StartMicInput(_lastMicDeviceNumber);
                    }
                    _logger.LogInformation("Microphone enabled for encoder audio.");
                }
                else
                {
                    // Remove microphone from all buses
                    _routingGraph.Route(AudioSourceType.Microphone, Array.Empty<AudioBus>());
                    _micInputService.Stop();
                    _logger.LogInformation("Microphone disabled for encoder audio.");
                }
            }

            /// <summary>
            /// Sets the microphone volume (0.0 to 1.0).
            /// </summary>
            public void SetMicVolume(double volume)
            {
                _micVolume = Math.Clamp(volume, 0.0, 1.0);
                _micInputService.SetVolume(_micVolume);
                _logger.LogInformation("Microphone volume set to {Volume:P0}", _micVolume);
            }

        private readonly CartPlayer _cartPlayer;
        private readonly ILogger<AudioService> _logger;
        private readonly IAudioDeviceResolver _deviceResolver;
        private readonly AudioRoutingGraph _routingGraph;
        private readonly VuMeterService _vuMeterService;
        private readonly MicInputService _micInputService;
        private IAudioEncoderTap? _encoderTap;
        private WaveFormat _encoderTapFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        private int _encoderDeviceId = -1;

        public AudioService(ILogger<AudioService>? logger = null, IAudioDeviceResolver? deviceResolver = null)
        {
            _logger = logger ?? AppLogger.CreateLogger<AudioService>();
            _deviceResolver = deviceResolver ?? new WaveAudioDeviceResolver();
            _routingGraph = new AudioRoutingGraph();
            _vuMeterService = new VuMeterService(_routingGraph);
            _vuMeterService.VuMetersUpdated += (_, reading) => VuMetersUpdated?.Invoke(this, reading);
            _micInputService = new MicInputService();
            _micInputService.LevelChanged += (_, level) => _vuMeterService.UpdateSourceLevel(AudioSourceType.Microphone, NormalizeLevel(level));
            _micInputService.SamplesAvailable += OnMicSamplesAvailable;

            _routingGraph.Route(AudioSourceType.DeckA, new[] { AudioBus.Program, AudioBus.Encoder });
            _routingGraph.Route(AudioSourceType.DeckB, new[] { AudioBus.Program, AudioBus.Encoder });
            _routingGraph.Route(AudioSourceType.Cartwall, new[] { AudioBus.Program, AudioBus.Encoder });

            DeckA = new AudioDeck(DeckIdentifier.A);
            DeckB = new AudioDeck(DeckIdentifier.B);
            DeckA.LevelChanged += level => _vuMeterService.UpdateSourceLevel(AudioSourceType.DeckA, NormalizeLevel(level));
            DeckB.LevelChanged += level => _vuMeterService.UpdateSourceLevel(AudioSourceType.DeckB, NormalizeLevel(level));
            DeckA.PlaybackStopped += () =>
            {
                _vuMeterService.UpdateSourceLevel(AudioSourceType.DeckA, 0);
                DeckPlaybackCompleted?.Invoke(this, DeckIdentifier.A);
            };
            DeckB.PlaybackStopped += () =>
            {
                _vuMeterService.UpdateSourceLevel(AudioSourceType.DeckB, 0);
                DeckPlaybackCompleted?.Invoke(this, DeckIdentifier.B);
            };

            _cartPlayer = new CartPlayer();
            _cartPlayer.LevelChanged += (_, level) => _vuMeterService.UpdateSourceLevel(AudioSourceType.Cartwall, NormalizeLevel(level));
            _logger.LogInformation("AudioService ready: DeckA={DeckA}, DeckB={DeckB}", DeckIdentifier.A, DeckIdentifier.B);
        }

        public AudioDeck DeckA { get; }
        public AudioDeck DeckB { get; }

        public event EventHandler<VuMeterReading>? VuMetersUpdated;
        public event EventHandler<DeckIdentifier>? DeckPlaybackCompleted;

        public AudioRoutingGraph RoutingGraph => _routingGraph;

        public void AttachEncoderTap(IAudioEncoderTap? encoderTap)
        {
            _encoderTap = encoderTap;
            _encoderTapFormat = encoderTap?.TargetFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            DeckA.SetEncoderTap(_encoderTapFormat, encoderTap?.CreateSourceTap(AudioSourceType.DeckA));
            DeckB.SetEncoderTap(_encoderTapFormat, encoderTap?.CreateSourceTap(AudioSourceType.DeckB));
            _cartPlayer.SetEncoderTap(_encoderTapFormat, encoderTap?.CreateSourceTap(AudioSourceType.Cartwall));
        }

        public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
        {
            var devices = _deviceResolver.GetPlaybackDevices();
            _logger.LogInformation("Enumerated {DeviceCount} playback devices", devices.Count);
            return devices;
        }

        public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
        {
            return _deviceResolver.GetInputDevices();
        }

        public void ApplyAudioSettings(AudioSettings? settings)
        {
            if (settings == null)
            {
                return;
            }

            var playbackDevices = _deviceResolver.GetPlaybackDevices();
            var inputDevices = _deviceResolver.GetInputDevices();

            int? appliedDeckA = null;
            if (TryResolvePlaybackDevice(playbackDevices, settings.DeckADeviceId, "Deck A", out var deckADevice))
            {
                SelectDeckOutputDevice(DeckIdentifier.A, deckADevice);
                appliedDeckA = deckADevice;
            }

            int? appliedDeckB = null;
            if (TryResolvePlaybackDevice(playbackDevices, settings.DeckBDeviceId, "Deck B", out var deckBDevice))
            {
                SelectDeckOutputDevice(DeckIdentifier.B, deckBDevice);
                appliedDeckB = deckBDevice;
            }

            int? appliedCart = null;
            if (TryResolvePlaybackDevice(playbackDevices, settings.CartWallDeviceId, "Cart Wall", out var cartDevice))
            {
                SelectCartOutputDevice(cartDevice);
                appliedCart = cartDevice;
            }

            if (!DeviceExists(playbackDevices, settings.EncoderDeviceId) && settings.EncoderDeviceId >= 0)
            {
                _logger.LogWarning("Configured encoder capture device {DeviceId} is unavailable. Stream capture will fall back to the system default loopback.", settings.EncoderDeviceId);
            }

            _encoderDeviceId = settings.EncoderDeviceId;

            if (TryResolveInputDevice(inputDevices, settings.MicInputDeviceId, out var micDeviceId))
            {
                _lastMicDeviceNumber = micDeviceId;
                // Only start mic if it's enabled - otherwise just remember the device
                if (_micEnabled)
                {
                    StartMicInput(micDeviceId);
                }
            }
            else
            {
                _lastMicDeviceNumber = -1;
                _micInputService.Stop();
            }
            var deckAVolumePercent = Math.Clamp(settings.DeckAVolumePercent, 0, 100);
            var deckBVolumePercent = Math.Clamp(settings.DeckBVolumePercent, 0, 100);
            SetDeckVolume(DeckIdentifier.A, deckAVolumePercent / 100d);
            SetDeckVolume(DeckIdentifier.B, deckBVolumePercent / 100d);
            var cartVolumePercent = Math.Clamp(settings.CartWallVolumePercent, 0, 100);
            SetCartVolume(cartVolumePercent / 100d);
        }

        public void StopDeck(DeckIdentifier deckId)
        {
            _logger.LogInformation($"StopDeck called for deck {deckId}");
            ResolveDeck(deckId).Stop();
        }

        public void PauseDeck(DeckIdentifier deckId)
        {
            _logger.LogInformation($"PauseDeck called for deck {deckId}");
            ResolveDeck(deckId).Pause();
        }

        public void SelectDeckOutputDevice(DeckIdentifier deckId, int deviceNumber)
        {
            _logger.LogInformation("Assigning deck {DeckId} to output device {Device}", deckId, deviceNumber);
            ResolveDeck(deckId).SelectOutputDevice(deviceNumber);
        }

        public void SelectCartOutputDevice(int deviceNumber)
        {
            _logger.LogInformation("Assigning cart wall to output device {Device}", deviceNumber);
            _cartPlayer.SelectOutputDevice(deviceNumber);
        }

        public void PlayDeck(DeckIdentifier deckId, string? filePath = null)
        {
            _logger.LogInformation("Play command for deck {DeckId} (override file: {HasOverride})", deckId, !string.IsNullOrWhiteSpace(filePath));
            ResolveDeck(deckId).Play(filePath);
        }

        public double SetDeckVolume(DeckIdentifier deckId, double volume)
        {
            var clamped = Math.Clamp(volume, 0d, 1d);
            var deck = ResolveDeck(deckId);
            return deck.SetVolume((float)clamped);
        }

        public double GetDeckVolume(DeckIdentifier deckId)
        {
            return ResolveDeck(deckId).Volume;
        }

        public double SetCartVolume(double volume)
        {
            var clamped = Math.Clamp(volume, 0d, 1d);
            _cartPlayer.SetVolume((float)clamped);
            return clamped;
        }

        public CartPlayback PlayCart(string filePath, bool loop = false, Action<TimeSpan>? elapsedCallback = null)
        {
            _logger.LogInformation("Triggering cart playback for {FilePath} (Loop={Loop})", filePath, loop);
            return _cartPlayer.Play(filePath, loop, elapsedCallback);
        }

        private AudioDeck ResolveDeck(DeckIdentifier deckId)
        {
            return deckId switch
            {
                DeckIdentifier.A => DeckA,
                DeckIdentifier.B => DeckB,
                _ => throw new ArgumentOutOfRangeException(nameof(deckId), deckId, "Unsupported deck identifier")
            };
        }

        public void Dispose()
        {
            DeckA.Dispose();
            DeckB.Dispose();
            _cartPlayer.Dispose();
            _micInputService.SamplesAvailable -= OnMicSamplesAvailable;
            _micInputService.Dispose();
            _vuMeterService.Dispose();
            _logger.LogInformation("AudioService disposed");
        }

        private static double NormalizeLevel(float level)
        {
            if (level < 0)
            {
                return 0;
            }

            if (level > 1)
            {
                return 1;
            }

            return level;
        }

        private bool TryResolvePlaybackDevice(IReadOnlyList<AudioDeviceInfo> devices, int requestedDeviceId, string role, out int resolvedDeviceId)
        {
            resolvedDeviceId = requestedDeviceId;

            if (requestedDeviceId == -1)
            {
                if (devices.Count == 0)
                {
                    _logger.LogWarning("No playback devices available; {Role} output muted.", role);
                    return false;
                }

                return true;
            }

            if (DeviceExists(devices, requestedDeviceId))
            {
                return true;
            }

            if (devices.Count == 0)
            {
                _logger.LogWarning("No playback devices available; {Role} output muted.", role);
                return false;
            }

            var fallback = devices[0];
            resolvedDeviceId = fallback.DeviceNumber;
            _logger.LogWarning("{Role} output device {Requested} unavailable. Falling back to {Fallback} ({Name}).", role, requestedDeviceId, fallback.DeviceNumber, fallback.ProductName);
            return true;
        }

        private static bool DeviceExists(IReadOnlyList<AudioDeviceInfo> devices, int deviceNumber)
        {
            if (deviceNumber < 0)
            {
                return devices.Count > 0;
            }

            return devices.Any(device => device.DeviceNumber == deviceNumber);
        }

        private bool TryResolveInputDevice(IReadOnlyList<AudioDeviceInfo> devices, int requestedDeviceId, out int resolvedDeviceId)
        {
            resolvedDeviceId = requestedDeviceId;

            if (requestedDeviceId < 0)
            {
                return false;
            }

            if (devices.Any(device => device.DeviceNumber == requestedDeviceId))
            {
                return true;
            }

            if (devices.Count == 0)
            {
                _logger.LogWarning("No input devices available; microphone monitoring disabled.");
                return false;
            }

            var fallback = devices[0];
            resolvedDeviceId = fallback.DeviceNumber;
            _logger.LogWarning("Mic input device {Requested} unavailable. Falling back to {Fallback} ({Name}).", requestedDeviceId, fallback.DeviceNumber, fallback.ProductName);
            return true;
        }

        private void StartMicInput(int deviceNumber)
        {
            try
            {
                _micInputService.Start(deviceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Microphone monitoring failed on device {Device}.", deviceNumber);
                _micInputService.Stop();
            }
        }

        private void OnMicSamplesAvailable(object? sender, MicSampleBlockEventArgs e)
        {
            try
            {
                var tap = _encoderTap;
                if (tap == null)
                {
                    return;
                }

                tap.SubmitMicrophoneSamples(e.Format, e.GetSamplesSpan());
            }
            finally
            {
                e.Dispose();
            }
        }
    }
}
