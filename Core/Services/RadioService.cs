using System;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    // Professional RadioService wrapper that exposes Play/Stop/NowPlaying
    public sealed class RadioService : IDisposable
    {
        private readonly TransportService _transport;
        private readonly EventBus _eventBus;
        private string? _nowPlaying;
        private readonly ILogger<RadioService> _logger;

        public RadioService(TransportService transport, EventBus eventBus, ILogger<RadioService>? logger = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? AppLogger.CreateLogger<RadioService>();
            _eventBus.Subscribe<DeckStateChangedEvent>(OnDeckStateChanged);
        }

        public string? NowPlaying
        {
            get => _nowPlaying;
            private set
            {
                if (_nowPlaying != value)
                {
                    _nowPlaying = value;
                    NowPlayingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? NowPlayingChanged;

        public DeckIdentifier ActiveDeck { get; set; } = DeckIdentifier.A;

        public void Play()
        {
            try
            {
                _transport.Play(ActiveDeck);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Play failed");
            }
        }

        public void Stop()
        {
            try
            {
                _transport.Stop(ActiveDeck);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stop failed");
            }
        }

        private void OnDeckStateChanged(DeckStateChangedEvent ev)
        {
            if (ev.QueueItem?.Track != null && ev.IsPlaying)
            {
                NowPlaying = $"{ev.QueueItem.Track.Title} — {ev.QueueItem.Track.Artist}";
            }
            else if (ev.QueueItem?.Track != null && !ev.IsPlaying)
            {
                NowPlaying = $"Paused: {ev.QueueItem.Track.Title} — {ev.QueueItem.Track.Artist}";
            }
            else
            {
                NowPlaying = string.Empty;
            }
        }

        public void Dispose()
        {
            // nothing to dispose owned here
        }
    }
}
