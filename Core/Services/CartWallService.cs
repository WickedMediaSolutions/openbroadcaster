using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class CartWallService : IDisposable
    {
        private readonly AudioService _audioService;
        private readonly CartPadStore _store;
        private readonly ObservableCollection<CartPad> _pads = new();
        private readonly Dictionary<int, PlaybackEntry> _active = new();
        private readonly Dictionary<int, TimeSpan> _totalDurations = new();
        private readonly SynchronizationContext? _syncContext;
        private readonly ILogger<CartWallService> _logger;
        private readonly string[] _defaultPalette =
        {
            "#FF2F3B52", "#FF1E2633", "#FF23303F", "#FF1A2432", "#FF2A3748", "#FF1C2836",
            "#FF2E3B4B", "#FF1F2936", "#FF2B3746", "#FF1D2633", "#FF2C3948", "#FF19212E"
        };

        public CartWallService(AudioService audioService, CartPadStore? store = null, int padCount = 12, SynchronizationContext? syncContext = null, ILogger<CartWallService>? logger = null)
        {
            if (padCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(padCount));
            }

            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _store = store ?? new CartPadStore();
            _syncContext = syncContext ?? SynchronizationContext.Current;
            _logger = logger ?? AppLogger.CreateLogger<CartWallService>();
            Pads = new ReadOnlyObservableCollection<CartPad>(_pads);
            Hydrate(padCount);
            _logger.LogInformation("Cart wall initialized with {PadCount} pads", padCount);
        }

        public ReadOnlyObservableCollection<CartPad> Pads { get; }

        public CartPad TogglePad(int padId)
        {
            var pad = GetPad(padId);
            if (!pad.HasAudio)
            {
                _logger.LogWarning("Cart pad {PadId} triggered without audio", padId);
                return pad;
            }

            if (pad.IsPlaying)
            {
                _logger.LogInformation("Stopping cart pad {PadId}", padId);
                StopPadPlayback(pad);
            }
            else
            {
                _logger.LogInformation("Starting cart pad {PadId} ({Label})", padId, pad.Label);
                StartPadPlayback(pad);
            }

            return pad;
        }

        public void AssignPadFile(int padId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A valid audio file is required", nameof(filePath));
            }

            var pad = GetPad(padId);
            StopPadPlayback(pad);

            pad.FilePath = filePath;
            pad.Label = Path.GetFileNameWithoutExtension(filePath);
            _logger.LogInformation("Assigned file {FilePath} to cart pad {PadId}", filePath, padId);
        }

        public void ClearPad(int padId)
        {
            var pad = GetPad(padId);
            StopPadPlayback(pad);
            pad.FilePath = string.Empty;
            _logger.LogInformation("Cleared assignment for cart pad {PadId}", padId);
        }

        public void Dispose()
        {
            foreach (var pad in _pads)
            {
                pad.PropertyChanged -= OnPadPropertyChanged;
            }

            foreach (var entry in _active.Values)
            {
                entry.Playback.Completed -= entry.Handler;
                entry.Playback.Dispose();
            }

            _active.Clear();
            PersistPads();
            _logger.LogInformation("Cart wall disposed; active playbacks flushed");
        }

        private void Hydrate(int padCount)
        {
            var snapshots = new Dictionary<int, CartPadStore.CartPadSnapshot>();
            foreach (var snapshot in _store.Load())
            {
                if (snapshot == null)
                {
                    continue;
                }

                if (snapshot.Id < 0)
                {
                    _logger.LogWarning("Ignoring cart pad snapshot with invalid id {PadId}", snapshot.Id);
                    continue;
                }

                if (snapshots.ContainsKey(snapshot.Id))
                {
                    _logger.LogWarning("Duplicate cart pad snapshot detected for id {PadId}; last write wins", snapshot.Id);
                }

                snapshots[snapshot.Id] = snapshot;
            }

            for (var id = 0; id < padCount; id++)
            {
                snapshots.TryGetValue(id, out var snapshot);
                var color = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.ColorHex)
                    ? snapshot.ColorHex
                    : _defaultPalette[id % _defaultPalette.Length];

                var label = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.Label)
                    ? snapshot.Label
                    : $"Cart {id + 1}";

                var pad = new CartPad(id, label, color);
                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.FilePath))
                {
                    pad.FilePath = snapshot.FilePath;
                }

                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.Hotkey))
                {
                    pad.Hotkey = snapshot.Hotkey;
                }

                if (snapshot != null)
                {
                    pad.LoopEnabled = snapshot.LoopEnabled;
                }

                pad.PropertyChanged += OnPadPropertyChanged;
                _pads.Add(pad);
            }

            _logger.LogInformation("Loaded {LoadedCount} cart pad snapshots", snapshots.Count);
        }

        private void OnPadPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CartPad.FilePath)
                || e.PropertyName == nameof(CartPad.Label)
                || e.PropertyName == nameof(CartPad.ColorHex)
                || e.PropertyName == nameof(CartPad.Hotkey)
                || e.PropertyName == nameof(CartPad.LoopEnabled))
            {
                PersistPads();
            }
        }

        public void SavePads()
        {
            PersistPads();
        }

        private CartPad GetPad(int padId)
        {
            var pad = _pads.FirstOrDefault(p => p.Id == padId);
            if (pad == null)
            {
                throw new ArgumentOutOfRangeException(nameof(padId), padId, "Unknown cart pad");
            }

            return pad;
        }

        private void StartPadPlayback(CartPad pad)
        {
            if (!pad.HasAudio || !File.Exists(pad.FilePath))
            {
                pad.IsPlaying = false;
                return;
            }

            try
            {
                // Get total duration for remaining time calculation
                var totalDuration = GetAudioDuration(pad.FilePath);
                _totalDurations[pad.Id] = totalDuration;
                _logger.LogInformation("Cart {PadId} total duration: {Duration}", pad.Id, totalDuration);

                var playback = _audioService.PlayCart(pad.FilePath, pad.LoopEnabled, elapsed => OnElapsedUpdate(pad.Id, elapsed));
                EventHandler handler = (_, __) => OnPlaybackCompleted(pad.Id);
                playback.Completed += handler;
                _active[pad.Id] = new PlaybackEntry(playback, handler);
                UpdatePadIsPlaying(pad, true);
            }
            catch
            {
                _logger.LogError("Failed to start cart pad {PadId} ({FilePath})", pad.Id, pad.FilePath);
                UpdatePadIsPlaying(pad, false);
            }
        }

        private void StopPadPlayback(CartPad pad)
        {
            if (_active.TryGetValue(pad.Id, out var entry))
            {
                entry.Playback.Completed -= entry.Handler;
                entry.Playback.Dispose();
                _active.Remove(pad.Id);
            }

            UpdatePadIsPlaying(pad, false);
        }

        private void OnPlaybackCompleted(int padId)
        {
            if (_active.TryGetValue(padId, out var entry))
            {
                entry.Playback.Completed -= entry.Handler;
                entry.Playback.Dispose();
                _active.Remove(padId);
            }

            var pad = _pads.FirstOrDefault(p => p.Id == padId);
            if (pad != null)
            {
                UpdatePadIsPlaying(pad, false);
            }

            _logger.LogInformation("Cart pad {PadId} playback completed", padId);
        }

        private void UpdatePadIsPlaying(CartPad pad, bool isPlaying)
        {
            if (_syncContext == null)
            {
                pad.IsPlaying = isPlaying;
                if (!isPlaying)
                {
                    pad.RemainingTime = string.Empty;
                }
                return;
            }

            _syncContext.Post(_ =>
            {
                pad.IsPlaying = isPlaying;
                if (!isPlaying)
                {
                    pad.RemainingTime = string.Empty;
                }
            }, null);
        }

        private void OnElapsedUpdate(int padId, TimeSpan elapsed)
        {
            var pad = _pads.FirstOrDefault(p => p.Id == padId);
            if (pad == null)
            {
                _logger.LogWarning("OnElapsedUpdate: pad {PadId} not found", padId);
                return;
            }
            
            if (!_totalDurations.TryGetValue(padId, out var total))
            {
                _logger.LogWarning("OnElapsedUpdate: total duration not found for pad {PadId}", padId);
                return;
            }

            var remaining = total - elapsed;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            var formatted = remaining.TotalHours >= 1
                ? remaining.ToString(@"h\:mm\:ss")
                : remaining.ToString(@"m\:ss");

            _logger.LogDebug("Cart {PadId} elapsed={Elapsed}, total={Total}, remaining={Remaining}", padId, elapsed, total, formatted);

            if (_syncContext == null)
            {
                pad.RemainingTime = formatted;
                return;
            }

            _syncContext.Post(_ => pad.RemainingTime = formatted, null);
        }

        private static TimeSpan GetAudioDuration(string filePath)
        {
            try
            {
                // Use the same factory that CartPlayer uses to get accurate duration
                using var reader = Audio.AudioFileReaderFactory.OpenRead(filePath);
                return reader.TotalTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private void PersistPads()
        {
            _store.Save(_pads);
        }

        private sealed record PlaybackEntry(CartPlayback Playback, EventHandler Handler);
    }
}
