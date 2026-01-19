using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Requests;
using Timer = System.Threading.Timer;

namespace OpenBroadcaster.Core.Services
{
    public sealed class TwitchIntegrationService : IDisposable
    {
        private static readonly TimeSpan SearchSessionTtl = TimeSpan.FromMinutes(5);

        private readonly QueueService _queueService;
        private readonly TransportService _transportService;
        private readonly LoyaltyLedger _loyaltyLedger;
        private readonly LibraryService _libraryService;
        private readonly TwitchIrcClient _ircClient;
        private readonly ILogger<TwitchIntegrationService> _logger;

        private readonly Dictionary<string, SearchSession> _searchSessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _requestCooldowns = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _recentActivity = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _stateLock = new();
        private readonly object _reconnectLock = new();

        private TwitchChatOptions? _options;
        private TwitchSettings _settings = new();
        private RequestSettings _requestSettings = new();
        private readonly RequestPolicyEvaluator _requestPolicy = new();
        private Timer? _loyaltyTimer;
        private Task? _reconnectTask;
        private CancellationToken _connectionToken = CancellationToken.None;
        private bool _shouldAutoReconnect;
        private int _reconnectAttempt;
        private bool _disposed;

        public TwitchIntegrationService(
            QueueService queueService,
            TransportService transportService,
            LoyaltyLedger loyaltyLedger,
            LibraryService libraryService,
            TwitchIrcClient? ircClient = null,
            ILogger<TwitchIntegrationService>? logger = null)
        {
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _transportService = transportService ?? throw new ArgumentNullException(nameof(transportService));
            _loyaltyLedger = loyaltyLedger ?? throw new ArgumentNullException(nameof(loyaltyLedger));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _logger = logger ?? AppLogger.CreateLogger<TwitchIntegrationService>();
            _ircClient = ircClient ?? new TwitchIrcClient(AppLogger.CreateLogger<TwitchIrcClient>());

            _ircClient.MessageReceived += OnMessageReceived;
            _ircClient.NoticeReceived += OnNoticeReceived;
            _ircClient.ConnectionClosed += OnConnectionClosed;
        }

        public event EventHandler<TwitchChatMessage>? ChatMessageReceived;
        public event EventHandler? QueueChanged;
        public event EventHandler<string>? StatusChanged;

        public void UpdateSettings(TwitchSettings settings)
        {
            _settings = settings?.Clone() ?? new TwitchSettings();
            RestartLoyaltyTimerIfActive();
        }

        public void UpdateRequestSettings(RequestSettings? settings)
        {
            _requestSettings = settings?.Clone() ?? new RequestSettings();
        }

        /// <summary>
        /// Announces a now playing message to Twitch chat.
        /// </summary>
        public void AnnounceNowPlaying(QueueItem? queueItem)
        {
            if (queueItem?.Track == null || !_ircClient.IsConnected)
            {
                return;
            }

            var track = queueItem.Track;
            string message;
            if (queueItem.HasRequester)
            {
                message = $"Now playing: {track.Artist} - {track.Title} requested by @{queueItem.RequestedBy}";
            }
            else
            {
                message = $"Now playing: {track.Artist} - {track.Title}";
            }

            RespondToChat(message, echo: true);
        }

        public async Task StartAsync(TwitchSettings settings, CancellationToken token)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var options = settings.ToChatOptions();
            if (!options.IsValid)
            {
                throw new ArgumentException("Twitch chat options are invalid.", nameof(options));
            }

            UpdateSettings(settings);
            _options = options;
            _connectionToken = token;
            _shouldAutoReconnect = true;
            _reconnectAttempt = 0;
            _logger.LogInformation("Connecting to Twitch IRC for channel {Channel}", options.NormalizedChannel);
            await ConnectInternalAsync(options, token, isReconnect: false).ConfigureAwait(false);
        }

        private async Task ConnectInternalAsync(TwitchChatOptions options, CancellationToken token, bool isReconnect)
        {
            await _ircClient.ConnectAsync(options, token).ConfigureAwait(false);
            StartLoyaltyTimer();
            var channel = options.NormalizedChannel;
            var status = isReconnect
                ? $"Reconnected to #{channel}."
                : $"Connected to #{channel}.";
            PublishStatus(status);
            PublishSystemMessage(isReconnect ? "Twitch chat bridge back online." : "Twitch chat bridge online.");
            var announcement = BuildOnlineAnnouncement();
            if (!string.IsNullOrWhiteSpace(announcement))
            {
                RespondToChat(announcement, echo: true);
            }
        }

        public async Task StopAsync()
        {
            _shouldAutoReconnect = false;
            StopLoyaltyTimer();
            await CancelReconnectLoopAsync().ConfigureAwait(false);

            if (!_ircClient.IsConnected)
            {
                _connectionToken = CancellationToken.None;
                return;
            }

            _logger.LogInformation("Disconnecting Twitch IRC bridge");
            await _ircClient.DisconnectAsync().ConfigureAwait(false);
            PublishStatus("Twitch chat bridge offline.");
            _connectionToken = CancellationToken.None;
        }

        private async Task CancelReconnectLoopAsync()
        {
            Task? pending;
            lock (_reconnectLock)
            {
                pending = _reconnectTask;
                _reconnectTask = null;
            }

            if (pending == null)
            {
                return;
            }

            try
            {
                await pending.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private void OnMessageReceived(object? sender, TwitchChatMessage e)
        {
            ChatMessageReceived?.Invoke(this, e);

            if (!e.IsFromBot)
            {
                AwardChatPoints(e.UserName);
                TrackActivity(e.UserName);
            }

            // Allow commands from broadcaster even if they're also the bot account (for testing)
            // Only skip if it's the bot AND not the broadcaster
            if (e.IsFromBot && !e.IsFromBroadcaster)
            {
                return;
            }
            
            if (!e.Message.StartsWith('!'))
            {
                return;
            }

            var payload = e.Message.Trim();;
            var splitIndex = payload.IndexOf(' ');
            var command = splitIndex > 0 ? payload[..splitIndex] : payload;
            var arguments = splitIndex > 0 ? payload[(splitIndex + 1)..].Trim() : string.Empty;
            _logger.LogInformation("Handling Twitch command {Command} from {User}", command, e.UserName);
            HandleCommand(e, command.ToLowerInvariant(), arguments);
        }

        internal void HandleCommand(TwitchChatMessage context, string command, string arguments)
        {
            if (TryHandleSelectionCommand(context, command))
            {
                return;
            }

            switch (command)
            {
                case "!s":
                case "!search":
                    HandleSearchCommand(context, arguments);
                    break;
                case "!pick":
                    HandlePickCommand(context, arguments);
                    break;
                case "!playnext":
                    HandlePlayNext(context, arguments);
                    break;
                case "!np":
                case "!song":
                    RespondToChat(GetNowPlayingMessage(), true);
                    break;
                case "!next":
                    RespondToChat(GetNextUpMessage(), true);
                    break;
                case "!addpoints":
                    HandleAddPoints(context, arguments);
                    break;
                case "!help":
                case "!commands":
                    RespondToChat($"Commands: !s <text> to search | !pick # or !1-!9 to request | !playnext # for priority ({_settings.PlayNextCost} {PointsLabel}) | !np now playing | !next up next", true);
                    break;
            }
        }

        private bool TryHandleSelectionCommand(TwitchChatMessage context, string command)
        {
            if (command.Length <= 1 || command[0] != '!')
            {
                return false;
            }

            if (!int.TryParse(command[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot))
            {
                return false;
            }

            HandleSelection(context, slot, priority: false);
            return true;
        }

        private void HandleSearchCommand(TwitchChatMessage context, string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                RespondToChat($"@{context.UserName} usage: !s <Artist or Title>", true);
                return;
            }

            var limit = Math.Max(1, _settings.SearchResultsLimit);
            var results = _libraryService.SearchTracks(arguments, limit);
            if (results.Count == 0)
            {
                RespondToChat($"@{context.UserName} no matches found for '{arguments}'.", true);
                return;
            }

            StoreSearchSession(context.UserName, arguments, results);
            RespondToChat(BuildSearchResponse(context.UserName, arguments, results), true);
        }

        private void HandleSelection(TwitchChatMessage context, int slot, bool priority)
        {
            if (!TryGetSearchSession(context.UserName, out var session))
            {
                RespondToChat($"@{context.UserName} search expired. Run !s again.", true);
                return;
            }

            if (slot < 1 || slot > session.Results.Count)
            {
                RespondToChat($"@{context.UserName} choose between 1 and {session.Results.Count}.", true);
                return;
            }

            if (IsOnCooldown(context.UserName, out var remainingCooldown))
            {
                RespondToChat($"@{context.UserName} wait {FormatCooldown(remainingCooldown)} before another request.", true);
                return;
            }

            if (!TryValidateRequestCapacity(context.UserName, out var rejectionReason))
            {
                RespondToChat($"@{context.UserName} {rejectionReason}", true);
                return;
            }

            var cost = priority ? _settings.PlayNextCost : _settings.RequestCost;
            if (!TrySpendPoints(context.UserName, cost, out var remainingPoints))
            {
                RespondToChat($"@{context.UserName} need {cost} {PointsLabel} (you have {remainingPoints}).", true);
                return;
            }

            var track = session.Results[slot - 1];
            EnqueueTrack(track, context.UserName, priority);
            lock (_stateLock)
            {
                _requestCooldowns[context.UserName] = DateTimeOffset.UtcNow;
            }

            var verb = priority ? "bumped" : "added";
            RespondToChat($"@{context.UserName} {verb} '{track.Title}'. ({remainingPoints} {PointsLabel} left)", true);
            _logger.LogInformation("Twitch selection {Slot} queued for {User} (priority={Priority})", slot, context.UserName, priority);
        }

        private void HandlePickCommand(TwitchChatMessage context, string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments) || !int.TryParse(arguments, NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot))
            {
                RespondToChat($"@{context.UserName} usage: !pick <number> (e.g., !pick 1)", true);
                return;
            }

            HandleSelection(context, slot, priority: false);
        }

        private void HandlePlayNext(TwitchChatMessage context, string arguments)
        {
            if (!string.IsNullOrWhiteSpace(arguments) && int.TryParse(arguments, NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot))
            {
                HandleSelection(context, slot, priority: true);
                return;
            }

            HandlePlayNextBump(context);
        }

        private void HandlePlayNextBump(TwitchChatMessage context)
        {
            if (_settings.PlayNextCost <= 0)
            {
                RespondToChat($"@{context.UserName} play next is disabled.", true);
                return;
            }

            if (!_loyaltyLedger.TryDebit(context.UserName, _settings.PlayNextCost, out var remaining))
            {
                RespondToChat($"@{context.UserName} need {_settings.PlayNextCost} {PointsLabel} to bump (you have {remaining}).", true);
                return;
            }

            var snapshot = _queueService.Snapshot();
            var targetIndex = -1;
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                if (string.Equals(snapshot[i].RequestedBy, context.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex <= 0)
            {
                _loyaltyLedger.AddPoints(context.UserName, _settings.PlayNextCost);
                RespondToChat($"@{context.UserName} no pending requests to bump.", true);
                return;
            }

            _queueService.Reorder(targetIndex, 0);
            QueueChanged?.Invoke(this, EventArgs.Empty);
            RespondToChat($"@{context.UserName} request bumped to the top. ({remaining} {PointsLabel} left)", true);
            _logger.LogInformation("playnext reorder succeeded for {User}", context.UserName);
        }

        private void HandleAddPoints(TwitchChatMessage context, string arguments)
        {
            if (!IsBroadcaster(context.UserName))
            {
                RespondToChat($"@{context.UserName} only the host can use !addpoints.", true);
                return;
            }

            var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            {
                RespondToChat("Usage: !addpoints <user> <amount>", true);
                return;
            }

            var target = parts[0].TrimStart('@');
            var total = _loyaltyLedger.AddPoints(target, amount);
            RespondToChat($"@{target} now has {total} {PointsLabel}.", true);
            _logger.LogInformation("{User} adjusted points for {Target} by {Amount}", context.UserName, target, amount);
        }

        private void EnqueueTrack(Track track, string requestedBy, bool priority)
        {
            var queueItem = new QueueItem(track, QueueSource.Twitch, ResolveRequestSourceLabel(), requestedBy);
            if (priority)
            {
                _queueService.EnqueueFront(queueItem);
            }
            else
            {
                _queueService.Enqueue(queueItem);
            }

            QueueChanged?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("Queue item created via Twitch request for {Track} (requested by {User}, priority={Priority})", track.Title, requestedBy, priority);
        }

        private string GetNowPlayingMessage()
        {
            var deck = _transportService.DeckA.IsPlaying ? _transportService.DeckA
                : _transportService.DeckB.IsPlaying ? _transportService.DeckB
                : null;

            if (deck?.CurrentQueueItem?.Track == null)
            {
                return "Nothing is currently on air.";
            }

            var track = deck.CurrentQueueItem.Track;
            if (deck.CurrentQueueItem.HasRequester)
            {
                return $"Now playing: {track.Artist} - {track.Title} requested by @{deck.CurrentQueueItem.RequestedBy}";
            }
            return $"Now playing: {track.Artist} - {track.Title}";
        }

        private string GetNextUpMessage()
        {
            var next = _queueService.Peek();
            if (next?.Track == null)
            {
                return "Queue is empty.";
            }

            if (next.HasRequester)
            {
                return $"Next up: {next.Track.Artist} - {next.Track.Title} requested by @{next.RequestedBy}";
            }
            return $"Next up: {next.Track.Artist} - {next.Track.Title}";
        }

        private void OnNoticeReceived(object? sender, string e)
        {
            PublishSystemMessage($"[IRC] {e}");
            _logger.LogWarning("Twitch notice: {Notice}", e);
            
            // Update status if it looks like an auth error
            if (e.Contains("Login", StringComparison.OrdinalIgnoreCase) || 
                e.Contains("auth", StringComparison.OrdinalIgnoreCase))
            {
                PublishStatus($"Auth issue: {e}", echoToChat: false);
            }
        }

        private void OnConnectionClosed(object? sender, EventArgs e)
        {
            StopLoyaltyTimer();
            PublishStatus("Twitch chat disconnected.");
            _logger.LogWarning("Twitch IRC connection closed");

            if (!_shouldAutoReconnect || _options == null || _connectionToken.IsCancellationRequested)
            {
                return;
            }

            lock (_reconnectLock)
            {
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                {
                    _reconnectTask = RunReconnectLoopAsync();
                }
            }
        }

        private async Task RunReconnectLoopAsync()
        {
            while (_shouldAutoReconnect && !_connectionToken.IsCancellationRequested)
            {
                var options = _options;
                if (options == null)
                {
                    return;
                }

                var attempt = Math.Max(0, _reconnectAttempt);
                var delaySeconds = Math.Min(Math.Pow(2, attempt) * 5, 60);
                var delay = TimeSpan.FromSeconds(Math.Max(5, delaySeconds));
                PublishStatus($"Reconnecting to #{options.NormalizedChannel} in {delay.TotalSeconds:0} seconds...", echoToChat: false);

                try
                {
                    await Task.Delay(delay, _connectionToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await ConnectInternalAsync(options, _connectionToken, isReconnect: true).ConfigureAwait(false);
                    _reconnectAttempt = 0;
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _reconnectAttempt++;
                    _logger.LogWarning(ex, "Twitch reconnect attempt {Attempt} failed", _reconnectAttempt);
                    PublishStatus($"Twitch reconnect failed: {ex.Message}", echoToChat: false);
                }
            }
        }

        private bool IsBroadcaster(string userName)
        {
            if (_options == null)
            {
                return false;
            }

            return string.Equals(userName, _options.NormalizedChannel, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildOnlineAnnouncement()
        {
            if (_settings == null)
            {
                return string.Empty;
            }

            var station = string.IsNullOrWhiteSpace(_settings.RadioStationName)
                ? _settings.Channel
                : _settings.RadioStationName;

            if (string.IsNullOrWhiteSpace(station))
            {
                return string.Empty;
            }

            return $"{station} is now online use !help for more information.";
        }

        private void RespondToChat(string text, bool echo)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var final = text.Length > 480 ? text[..480] + "…" : text;
            _ = _ircClient.SendMessageAsync(final);

            if (echo)
            {
                PublishSystemMessage(final);
            }
        }

        private void PublishSystemMessage(string text)
        {
            var author = _options?.UserName ?? "StudioBot";
            var payload = new TwitchChatMessage(author, text, DateTime.UtcNow, true, false, true);
            ChatMessageReceived?.Invoke(this, payload);
        }

        private void PublishStatus(string status, bool echoToChat = true)
        {
            StatusChanged?.Invoke(this, status);
            if (echoToChat)
            {
                PublishSystemMessage(status);
            }
        }

        private bool TrySpendPoints(string userName, int cost, out int remaining)
        {
            remaining = 0;
            if (cost <= 0)
            {
                remaining = _loyaltyLedger.GetPoints(userName);
                return true;
            }

            return _loyaltyLedger.TryDebit(userName, cost, out remaining);
        }

        private void AwardChatPoints(string userName)
        {
            if (_settings.ChatMessageAwardPoints <= 0 || string.IsNullOrWhiteSpace(userName))
            {
                return;
            }

            _loyaltyLedger.AddPoints(userName, _settings.ChatMessageAwardPoints);
        }

        private void TrackActivity(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return;
            }

            lock (_stateLock)
            {
                _recentActivity[userName] = DateTimeOffset.UtcNow;
                var cutoff = DateTimeOffset.UtcNow - GetActivityWindow();
                PruneInactiveUsersLocked(cutoff);
            }
        }

        private bool IsOnCooldown(string userName, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (_settings.RequestCooldownSeconds <= 0 || string.IsNullOrWhiteSpace(userName))
            {
                return false;
            }

            lock (_stateLock)
            {
                if (!_requestCooldowns.TryGetValue(userName, out var last))
                {
                    return false;
                }

                var elapsed = DateTimeOffset.UtcNow - last;
                var cooldown = TimeSpan.FromSeconds(_settings.RequestCooldownSeconds);
                if (elapsed >= cooldown)
                {
                    _requestCooldowns.Remove(userName);
                    return false;
                }

                remaining = cooldown - elapsed;
                return true;
            }
        }

        private string BuildSearchResponse(string userName, string query, IReadOnlyList<Track> results)
        {
            var builder = new StringBuilder();
            builder.Append('@').Append(userName).Append(" results for '").Append(query).Append("': ");
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                var track = results[i];
                builder.Append(i + 1)
                    .Append(") ")
                    .Append(track.Title)
                    .Append(" - ")
                    .Append(track.Artist)
                    .Append(' ')
                    .Append('(')
                    .Append(FormatDuration(track.Duration))
                    .Append(')');
            }

            builder.Append(". Use !1-!")
                .Append(Math.Min(results.Count, Math.Max(1, _settings.SearchResultsLimit)))
                .Append(" to queue or !playnext <n> to jump the line.");
            return builder.ToString();
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return "--:--";
            }

            return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
        }

        private bool TryGetSearchSession(string userName, out SearchSession session)
        {
            session = default!;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return false;
            }

            lock (_stateLock)
            {
                CleanupExpiredSessionsLocked();
                if (_searchSessions.TryGetValue(userName, out var existing))
                {
                    session = existing;
                    return true;
                }
            }

            return false;
        }

        private void StoreSearchSession(string userName, string query, IReadOnlyList<Track> results)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return;
            }

            var snapshot = results.ToArray();
            lock (_stateLock)
            {
                CleanupExpiredSessionsLocked();
                _searchSessions[userName] = new SearchSession(query, snapshot, DateTimeOffset.UtcNow);
            }
        }

        private void CleanupExpiredSessionsLocked()
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _searchSessions
                .Where(kvp => now - kvp.Value.CreatedUtc > SearchSessionTtl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _searchSessions.Remove(key);
            }
        }

        private void PruneInactiveUsersLocked(DateTimeOffset cutoff)
        {
            var stale = _recentActivity
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in stale)
            {
                _recentActivity.Remove(key);
            }
        }

        private void StartLoyaltyTimer()
        {
            StopLoyaltyTimer();

            if (_settings.IdleAwardPoints <= 0)
            {
                return;
            }

            var interval = GetIdleInterval();
            _loyaltyTimer = new Timer(AwardIdlePoints, null, interval, interval);
        }

        private void AwardIdlePoints(object? state)
        {
            if (_settings.IdleAwardPoints <= 0)
            {
                return;
            }

            try
            {
                List<string> recipients;
                var cutoff = DateTimeOffset.UtcNow - GetActivityWindow();
                lock (_stateLock)
                {
                    recipients = _recentActivity
                        .Where(kvp => kvp.Value >= cutoff)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    PruneInactiveUsersLocked(cutoff);
                }

                foreach (var user in recipients)
                {
                    _loyaltyLedger.AddPoints(user, _settings.IdleAwardPoints);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Idle loyalty award failed");
            }
        }

        private void StopLoyaltyTimer()
        {
            _loyaltyTimer?.Dispose();
            _loyaltyTimer = null;
        }

        private void RestartLoyaltyTimerIfActive()
        {
            if (_loyaltyTimer != null)
            {
                StartLoyaltyTimer();
            }
        }

        private TimeSpan GetIdleInterval()
        {
            return TimeSpan.FromMinutes(Math.Max(1, _settings.IdleAwardIntervalMinutes));
        }

        private TimeSpan GetActivityWindow()
        {
            return TimeSpan.FromMinutes(Math.Max(1, _settings.IdleAwardIntervalMinutes) * 2);
        }

        private static string FormatCooldown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                return "0s";
            }

            if (remaining.TotalMinutes >= 1)
            {
                return $"{Math.Ceiling(remaining.TotalMinutes)}m";
            }

            return $"{Math.Ceiling(remaining.TotalSeconds)}s";
        }

        private string PointsLabel => string.IsNullOrWhiteSpace(_settings.PointsName) ? "Sheckles" : _settings.PointsName;

        private bool TryValidateRequestCapacity(string userName, out string rejection)
        {
            var snapshot = _queueService.Snapshot();
            return _requestPolicy.TryValidate(_requestSettings, snapshot, userName, out rejection);
        }

        private string ResolveRequestSourceLabel()
        {
            if (string.IsNullOrWhiteSpace(_requestSettings.SourceLabel))
            {
                return "Twitch Chat";
            }

            return _requestSettings.SourceLabel.Trim();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shouldAutoReconnect = false;
            var pendingReconnect = _reconnectTask;
            if (pendingReconnect != null && !pendingReconnect.IsCompleted)
            {
                try
                {
                    pendingReconnect.Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                }
            }
            StopLoyaltyTimer();
            _ircClient.MessageReceived -= OnMessageReceived;
            _ircClient.NoticeReceived -= OnNoticeReceived;
            _ircClient.ConnectionClosed -= OnConnectionClosed;
            _ircClient.Dispose();
            _logger.LogInformation("Disposed TwitchIntegrationService");
        }

        private sealed record SearchSession(string Query, IReadOnlyList<Track> Results, DateTimeOffset CreatedUtc);
    }
}
