using System.Collections.Concurrent;
using System.Net.WebSockets;
using OpenBroadcaster.RelayService.Contracts;
using OpenBroadcaster.RelayService.Contracts.Payloads;

namespace OpenBroadcaster.RelayService.Services
{
    /// <summary>
    /// Represents an active station connection.
    /// </summary>
    public sealed class StationConnection
    {
        public string StationId { get; }
        public string StationName { get; }
        public WebSocket WebSocket { get; }
        public DateTimeOffset ConnectedAt { get; }
        public DateTimeOffset LastHeartbeat { get; set; }
        public string? ClientVersion { get; set; }
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Cached now playing state for this station.
        /// </summary>
        public NowPlayingPayload? CachedNowPlaying { get; set; }

        /// <summary>
        /// Cached queue state for this station.
        /// </summary>
        public QueueStatePayload? CachedQueueState { get; set; }

        /// <summary>
        /// Pending requests waiting for responses.
        /// Key = CorrelationId, Value = TaskCompletionSource for the response.
        /// </summary>
        public ConcurrentDictionary<string, PendingRequest> PendingRequests { get; } = new();

        public StationConnection(string stationId, string stationName, WebSocket webSocket)
        {
            StationId = stationId ?? throw new ArgumentNullException(nameof(stationId));
            StationName = stationName ?? stationId;
            WebSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            ConnectedAt = DateTimeOffset.UtcNow;
            LastHeartbeat = DateTimeOffset.UtcNow;
        }

        public bool IsConnected => WebSocket.State == WebSocketState.Open;
    }

    /// <summary>
    /// Represents a pending request awaiting a response.
    /// </summary>
    public sealed class PendingRequest
    {
        public string CorrelationId { get; }
        public string ExpectedResponseType { get; }
        public TaskCompletionSource<MessageEnvelope?> CompletionSource { get; }
        public DateTimeOffset CreatedAt { get; }
        public CancellationTokenRegistration CancellationRegistration { get; set; }

        public PendingRequest(string correlationId, string expectedResponseType)
        {
            CorrelationId = correlationId;
            ExpectedResponseType = expectedResponseType;
            CompletionSource = new TaskCompletionSource<MessageEnvelope?>(TaskCreationOptions.RunContinuationsAsynchronously);
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Manages active station connections.
    /// 
    /// DESIGN RATIONALE:
    /// - Thread-safe dictionary for concurrent access
    /// - Single connection per station (configurable)
    /// - Cached state for fast REST responses
    /// - Request/response correlation for async operations
    /// </summary>
    public sealed class StationConnectionManager
    {
        private readonly ConcurrentDictionary<string, StationConnection> _connections = new();
        private readonly ILogger<StationConnectionManager> _logger;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);

        public StationConnectionManager(ILogger<StationConnectionManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets all connected stations.
        /// </summary>
        public IEnumerable<StationConnection> GetAllConnections()
        {
            return _connections.Values.Where(c => c.IsConnected);
        }

        /// <summary>
        /// Gets a specific station connection.
        /// </summary>
        public StationConnection? GetConnection(string stationId)
        {
            if (_connections.TryGetValue(stationId, out var connection) && connection.IsConnected)
            {
                return connection;
            }
            return null;
        }

        /// <summary>
        /// Checks if a station is currently connected.
        /// </summary>
        public bool IsStationConnected(string stationId)
        {
            return GetConnection(stationId) != null;
        }

        /// <summary>
        /// Registers a new station connection.
        /// </summary>
        public bool RegisterConnection(StationConnection connection)
        {
            // Remove any existing disconnected connection
            if (_connections.TryGetValue(connection.StationId, out var existing) && !existing.IsConnected)
            {
                _connections.TryRemove(connection.StationId, out _);
            }

            if (_connections.TryAdd(connection.StationId, connection))
            {
                _logger.LogInformation("Station '{StationId}' ({StationName}) connected", 
                    connection.StationId, connection.StationName);
                return true;
            }

            _logger.LogWarning("Station '{StationId}' already has an active connection", connection.StationId);
            return false;
        }

        /// <summary>
        /// Removes a station connection.
        /// </summary>
        public void RemoveConnection(string stationId)
        {
            if (_connections.TryRemove(stationId, out var connection))
            {
                _logger.LogInformation("Station '{StationId}' disconnected", stationId);

                // Cancel all pending requests
                foreach (var pending in connection.PendingRequests.Values)
                {
                    pending.CompletionSource.TrySetCanceled();
                }
            }
        }

        /// <summary>
        /// Updates cached now playing state for a station.
        /// </summary>
        public void UpdateNowPlaying(string stationId, NowPlayingPayload payload)
        {
            if (_connections.TryGetValue(stationId, out var connection))
            {
                connection.CachedNowPlaying = payload;
                _logger.LogDebug("Updated now playing for station '{StationId}': {Title} - {Artist}",
                    stationId, payload.Title, payload.Artist);
            }
        }

        /// <summary>
        /// Updates cached queue state for a station.
        /// </summary>
        public void UpdateQueueState(string stationId, QueueStatePayload payload)
        {
            if (_connections.TryGetValue(stationId, out var connection))
            {
                connection.CachedQueueState = payload;
                _logger.LogDebug("Updated queue state for station '{StationId}': {Count} items",
                    stationId, payload.TotalCount);
            }
        }

        /// <summary>
        /// Updates heartbeat timestamp for a station.
        /// </summary>
        public void UpdateHeartbeat(string stationId)
        {
            if (_connections.TryGetValue(stationId, out var connection))
            {
                connection.LastHeartbeat = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// Sends a message to a station and waits for a response.
        /// </summary>
        public async Task<MessageEnvelope?> SendAndWaitForResponseAsync(
            string stationId,
            MessageEnvelope request,
            string expectedResponseType,
            CancellationToken cancellationToken = default)
        {
            var connection = GetConnection(stationId);
            if (connection == null)
            {
                return null;
            }

            // Generate correlation ID if not set
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
            request.CorrelationId = correlationId;

            var pending = new PendingRequest(correlationId, expectedResponseType);
            connection.PendingRequests[correlationId] = pending;

            try
            {
                // Set up timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_requestTimeout);

                pending.CancellationRegistration = cts.Token.Register(() =>
                {
                    pending.CompletionSource.TrySetCanceled();
                });

                // Send the request
                await SendMessageAsync(connection, request, cancellationToken);

                // Wait for response
                return await pending.CompletionSource.Task;
            }
            finally
            {
                connection.PendingRequests.TryRemove(correlationId, out _);
                pending.CancellationRegistration.Dispose();
            }
        }

        /// <summary>
        /// Completes a pending request with a response.
        /// </summary>
        public bool CompleteRequest(string stationId, MessageEnvelope response)
        {
            if (string.IsNullOrEmpty(response.CorrelationId))
            {
                return false;
            }

            if (_connections.TryGetValue(stationId, out var connection) &&
                connection.PendingRequests.TryGetValue(response.CorrelationId, out var pending))
            {
                return pending.CompletionSource.TrySetResult(response);
            }

            return false;
        }

        /// <summary>
        /// Sends a message to a station without waiting for response.
        /// </summary>
        public async Task SendMessageAsync(StationConnection connection, MessageEnvelope message, CancellationToken cancellationToken = default)
        {
            if (!connection.IsConnected)
            {
                _logger.LogWarning("Cannot send message to disconnected station '{StationId}'", connection.StationId);
                return;
            }

            try
            {
                var json = message.ToJson();
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await connection.WebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to station '{StationId}'", connection.StationId);
            }
        }

        /// <summary>
        /// Broadcasts a message to all connected stations.
        /// </summary>
        public async Task BroadcastAsync(MessageEnvelope message, CancellationToken cancellationToken = default)
        {
            var tasks = GetAllConnections()
                .Select(c => SendMessageAsync(c, message, cancellationToken));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets connection statistics.
        /// </summary>
        public (int Total, int Connected, int Authenticated) GetStats()
        {
            var all = _connections.Values.ToList();
            return (
                all.Count,
                all.Count(c => c.IsConnected),
                all.Count(c => c.IsConnected && c.IsAuthenticated)
            );
        }
    }
}
