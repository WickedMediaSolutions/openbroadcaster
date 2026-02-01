using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace OpenBroadcaster.Avalonia.Services
{
    // RadioService that attempts to load the existing OpenBroadcaster core at runtime via reflection.
    // It exposes Play/Stop and NowPlaying and polls deck state periodically when available.
    public class RadioService
    {
        private readonly object _lock = new();
        private System.Threading.Timer? _pollTimer;
        private Assembly? _coreAssembly;
        private object? _transportServiceInstance;
        private Type? _transportServiceType;
        private Type? _deckType;
        private string? _nowPlaying;

        public RadioService()
        {
            // Start polling to detect and bind to the core app if its assembly is present.
            _pollTimer = new System.Threading.Timer(_ => PollForCore(), null, 0, 1000);
            ActiveDeck = 0; // 0 == Deck A
        }

        public int ActiveDeck { get; set; }

        public string? NowPlaying
        {
            get => _nowPlaying;
            private set
            {
                _nowPlaying = value;
                NowPlayingChanged?.Invoke();
            }
        }

        public event Action? NowPlayingChanged;

        public void Play()
        {
            if (_transportServiceInstance == null || _transportServiceType == null)
            {
                return;
            }

            try
            {
                var method = _transportServiceType.GetMethod("Play", new[] { typeof(object) });
                if (method == null)
                {
                    // Try non-object signature with enum parameter
                    method = _transportServiceType.GetMethod("Play");
                }

                if (method != null)
                {
                    // Prefer calling Play(DeckIdentifier) if available
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1)
                    {
                        var deckEnum = FindDeckIdentifierType();
                        if (deckEnum != null && deckEnum.IsEnum)
                        {
                            var deckValue = Enum.ToObject(deckEnum, ActiveDeck == 0 ? 0 : 1);
                            method.Invoke(_transportServiceInstance, new[] { deckValue });
                        }
                        else
                        {
                            method.Invoke(_transportServiceInstance, new object?[] { null });
                        }
                    }
                    else
                    {
                        method.Invoke(_transportServiceInstance, null);
                    }
                }
            }
            catch
            {
                // swallow
            }
        }

        public void Stop()
        {
            if (_transportServiceInstance == null || _transportServiceType == null)
            {
                return;
            }

            try
            {
                var method = _transportServiceType.GetMethod("Stop", new[] { typeof(object) }) ?? _transportServiceType.GetMethod("Stop");
                if (method != null)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1)
                    {
                        var deckEnum = FindDeckIdentifierType();
                        if (deckEnum != null && deckEnum.IsEnum)
                        {
                            var deckValue = Enum.ToObject(deckEnum, ActiveDeck == 0 ? 0 : 1);
                            method.Invoke(_transportServiceInstance, new[] { deckValue });
                        }
                        else
                        {
                            method.Invoke(_transportServiceInstance, new object?[] { null });
                        }
                    }
                    else
                    {
                        method.Invoke(_transportServiceInstance, null);
                    }
                }
            }
            catch
            {
            }
        }

        private void PollForCore()
        {
            try
            {
                if (_coreAssembly == null)
                {
                    var probe = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows", "OpenBroadcaster.dll");
                    if (!File.Exists(probe))
                    {
                        return;
                    }

                    _coreAssembly = Assembly.LoadFrom(probe);
                    _transportServiceType = _coreAssembly.GetType("OpenBroadcaster.Core.Services.TransportService");
                    _deckType = _coreAssembly.GetType("OpenBroadcaster.Core.Models.Deck");

                    var queueType = _coreAssembly.GetType("OpenBroadcaster.Core.Services.QueueService");
                    var eventBusType = _coreAssembly.GetType("OpenBroadcaster.Core.Messaging.EventBus");
                    var audioServiceType = _coreAssembly.GetType("OpenBroadcaster.Core.Services.AudioService");

                    if (_transportServiceType == null || queueType == null || eventBusType == null)
                    {
                        return;
                    }

                    var eventBus = Activator.CreateInstance(eventBusType);
                    var queue = Activator.CreateInstance(queueType);
                    var audio = audioServiceType != null ? Activator.CreateInstance(audioServiceType) : null;

                    _transportServiceInstance = Activator.CreateInstance(_transportServiceType, eventBus, queue, audio);

                    // Start a tighter polling loop for deck state
                    _pollTimer?.Change(0, 250);
                }
                else
                {
                    // Query deck state
                    if (_transportServiceInstance != null && _transportServiceType != null)
                    {
                        var deckProp = _transportServiceType.GetProperty("DeckA");
                        if (deckProp != null)
                        {
                            var deck = deckProp.GetValue(_transportServiceInstance);
                            if (deck != null)
                            {
                                var current = deck.GetType().GetProperty("CurrentQueueItem")?.GetValue(deck);
                                if (current != null)
                                {
                                    var track = current.GetType().GetProperty("Track")?.GetValue(current);
                                    if (track != null)
                                    {
                                        var title = track.GetType().GetProperty("Title")?.GetValue(track)?.ToString() ?? string.Empty;
                                        var artist = track.GetType().GetProperty("Artist")?.GetValue(track)?.ToString() ?? string.Empty;
                                        NowPlaying = string.IsNullOrWhiteSpace(title) ? string.Empty : $"{title} — {artist}";
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    NowPlaying = string.Empty;
                }
            }
            catch
            {
                // swallow
            }
        }

        private Type? FindDeckIdentifierType()
        {
            if (_coreAssembly == null) return null;
            return _coreAssembly.GetType("OpenBroadcaster.Core.Models.DeckIdentifier");
        }
    }
}
