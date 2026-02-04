using System;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class TransportService : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly QueueService _queueService;
        private readonly AudioService? _audioService;
        private readonly ILogger<TransportService> _logger;
        private readonly Timer _deckATelemetryTimer;
        private readonly Timer _deckBTelemetryTimer;
        private readonly TimeSpan _telemetryInterval;
        private readonly object _deckALock = new();
        private readonly object _deckBLock = new();
        private CancellationTokenSource? _deckACts;
        private CancellationTokenSource? _deckBCts;
        private bool _disposed;

        /// <summary>
        /// Flag to indicate a manual skip is in progress. When true, the PlaybackStopped event
        /// should not trigger auto-advance since the skip operation will handle loading the next track.
        /// </summary>
        public bool IsSkipping { get; set; }

        public TransportService(IEventBus eventBus, QueueService queueService, AudioService? audioService = null, ILogger<TransportService>? logger = null, TimeSpan? telemetryInterval = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _audioService = audioService;
            _logger = logger ?? AppLogger.CreateLogger<TransportService>();
            _telemetryInterval = telemetryInterval ?? TimeSpan.FromMilliseconds(250);
            DeckA = new Deck("Deck A");
            DeckB = new Deck("Deck B");
            _deckATelemetryTimer = CreateTelemetryTimer(DeckIdentifier.A);
            _deckBTelemetryTimer = CreateTelemetryTimer(DeckIdentifier.B);
            _logger.LogInformation("TransportService initialized with two decks");
        }

        public Deck DeckA { get; }
        public Deck DeckB { get; }
        private void LoadDeck(DeckIdentifier deckId, QueueItem queueItem)
        {
            var deck = Resolve(deckId);
            StopTelemetry(deckId);
            deck.Load(queueItem);
            _logger.LogInformation("Loaded {Title} into deck {DeckId}", queueItem.Track.Title, deckId);
            Publish(deckId, deck);
        }

        /// <summary>
        /// Directly load a track to a specific deck (for drag-and-drop from library).
        /// </summary>
        public void LoadTrackToDeck(DeckIdentifier deckId, Track track)
        {
            var queueItem = new QueueItem(track, QueueSource.Manual, "Library", "Host");
            LoadDeck(deckId, queueItem);
        }

        public void Unload(DeckIdentifier deckId)
        {
            var deck = Resolve(deckId);
            deck.Unload();
            StopTelemetry(deckId);
             _logger.LogInformation("Unloaded deck {DeckId}", deckId);
            Publish(deckId, deck);
        }

        public void Play(DeckIdentifier deckId)
        {
            var lockObj = deckId == DeckIdentifier.A ? _deckALock : _deckBLock;
            bool needsAudioStart = false;
            bool needsAudioResume = false;
            string? filePath = null;
            
            lock (lockObj)
            {
                var deck = Resolve(deckId);
                
                // If already playing, do nothing
                if (deck.Status == DeckStatus.Playing)
                {
                    _logger.LogDebug("Deck {DeckId} is already playing, ignoring play command", deckId);
                    return;
                }
                
                // If paused, just resume (don't reload file)
                if (deck.Status == DeckStatus.Paused)
                {
                    deck.Play();
                    StartTelemetry(deckId);
                    needsAudioResume = true;
                    _logger.LogInformation("Resuming playback on deck {DeckId}", deckId);
                    Publish(deckId, deck);
                }
                else
                {
                    // Deck is stopped/ready - need to load if no track
                    if (deck.CurrentQueueItem == null)
                    {
                        if (!TryLoadFromQueue(deckId))
                        {
                            _logger.LogWarning("Deck {DeckId} has no track to play and queue is empty.", deckId);
                            return;
                        }

                        deck = Resolve(deckId);
                    }

                    deck.Play();
                    StartTelemetry(deckId);
                    needsAudioStart = true;
                    filePath = deck.CurrentQueueItem?.Track?.FilePath;

                    // Cancel any previous playback task
                    if (deckId == DeckIdentifier.A)
                    {
                        _deckACts?.Cancel();
                        _deckACts = new CancellationTokenSource();
                    }
                    else
                    {
                        _deckBCts?.Cancel();
                        _deckBCts = new CancellationTokenSource();
                    }

                    _logger.LogInformation("Play triggered on deck {DeckId}", deckId);
                    Publish(deckId, deck);
                }
            }

            // Handle audio outside the lock
            if (needsAudioResume && _audioService != null)
            {
                try
                {
                    _audioService.PlayDeck(deckId); // Resume without file path
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error resuming audio for deck {DeckId}", deckId);
                }
            }
            else if (needsAudioStart && _audioService != null && !string.IsNullOrWhiteSpace(filePath))
            {
                var cts = deckId == DeckIdentifier.A ? _deckACts : _deckBCts;
                if (cts != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Delay to ensure deck state is set and any stop commands can take effect
                            await Task.Delay(50, cts.Token);
                            if (!cts.Token.IsCancellationRequested)
                            {
                                _audioService.PlayDeck(deckId, filePath);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when stopping quickly
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Audio playback failed for deck {DeckId} (Source={Source})", deckId, filePath);
                        }
                    }, cts.Token);
                }
            }
        }

        public void Pause(DeckIdentifier deckId)
        {
            var lockObj = deckId == DeckIdentifier.A ? _deckALock : _deckBLock;
            
            lock (lockObj)
            {
                // Cancel any pending playback
                if (deckId == DeckIdentifier.A)
                {
                    _deckACts?.Cancel();
                    _deckACts?.Dispose();
                    _deckACts = null;
                }
                else
                {
                    _deckBCts?.Cancel();
                    _deckBCts?.Dispose();
                    _deckBCts = null;
                }

                var deck = Resolve(deckId);
                deck.Pause();
                StopTelemetry(deckId);
                _logger.LogInformation("Pause triggered on deck {DeckId}", deckId);
                Publish(deckId, deck);
            }

            // Pause audio outside the lock to prevent deadlocks
            try
            {
                _audioService?.PauseDeck(deckId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error pausing audio for deck {DeckId}", deckId);
            }
        }

        public void Stop(DeckIdentifier deckId)
        {
            var lockObj = deckId == DeckIdentifier.A ? _deckALock : _deckBLock;
            
            lock (lockObj)
            {
                // Cancel any pending playback
                if (deckId == DeckIdentifier.A)
                {
                    _deckACts?.Cancel();
                    _deckACts?.Dispose();
                    _deckACts = null;
                }
                else
                {
                    _deckBCts?.Cancel();
                    _deckBCts?.Dispose();
                    _deckBCts = null;
                }

                var deck = Resolve(deckId);
                deck.Stop();
                StopTelemetry(deckId);

                _logger.LogInformation("Stop triggered on deck {DeckId}", deckId);
                Publish(deckId, deck);
            }

            // Stop audio outside the lock to prevent deadlocks
            try
            {
                _audioService?.StopDeck(deckId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping audio for deck {DeckId}", deckId);
            }
        }

        public QueueItem? RequestNextFromQueue(DeckIdentifier deckId)
        {
            if (!TryLoadFromQueue(deckId))
            {
                return null;
            }

            return Resolve(deckId).CurrentQueueItem;
        }

        private Deck Resolve(DeckIdentifier deckId)
        {
            return deckId switch
            {
                DeckIdentifier.A => DeckA,
                DeckIdentifier.B => DeckB,
                _ => throw new ArgumentOutOfRangeException(nameof(deckId), deckId, "Unsupported deck identifier")
            };
        }

        private void Publish(DeckIdentifier deckId, Deck deck)
        {
            _eventBus.Publish(new DeckStateChangedEvent(deckId, deck.CurrentQueueItem, deck.IsPlaying, deck.Elapsed, deck.Remaining, deck.Status));
            _logger.LogDebug("Published deck {DeckId} state (Status={Status}, Elapsed={Elapsed})", deckId, deck.Status, deck.Elapsed);
        }

        private bool TryLoadFromQueue(DeckIdentifier deckId)
        {
            var next = _queueService.Dequeue();
            if (next == null)
            {
                _logger.LogWarning("Queue is empty, cannot load deck {DeckId}", deckId);
                return false;
            }

            LoadDeck(deckId, next);
            return true;
        }

        private Timer CreateTelemetryTimer(DeckIdentifier deckId)
        {
            var timer = new Timer(_telemetryInterval.TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = false
            };
            timer.Elapsed += (_, _) => Publish(deckId, Resolve(deckId));
            return timer;
        }

        private Timer ResolveTimer(DeckIdentifier deckId)
        {
            return deckId switch
            {
                DeckIdentifier.A => _deckATelemetryTimer,
                DeckIdentifier.B => _deckBTelemetryTimer,
                _ => throw new ArgumentOutOfRangeException(nameof(deckId), deckId, "Unsupported deck identifier")
            };
        }

        private void StartTelemetry(DeckIdentifier deckId)
        {
            ResolveTimer(deckId).Start();
        }

        private void StopTelemetry(DeckIdentifier deckId)
        {
            var timer = ResolveTimer(deckId);
            if (timer.Enabled)
            {
                timer.Stop();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _deckACts?.Cancel();
            _deckACts?.Dispose();
            _deckBCts?.Cancel();
            _deckBCts?.Dispose();
            StopTelemetry(DeckIdentifier.A);
            StopTelemetry(DeckIdentifier.B);
            _deckATelemetryTimer.Dispose();
            _deckBTelemetryTimer.Dispose();
            _disposed = true;
        }
    }
}
