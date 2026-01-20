using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Relay.Contracts;
using OpenBroadcaster.Core.Relay.Contracts.Payloads;

namespace OpenBroadcaster.Core.Relay.Client
{
    /// <summary>
    /// WebSocket client for connecting to the OpenBroadcaster Relay Service.
    /// 
    /// DESIGN RATIONALE:
    /// - Uses native System.Net.WebSockets only (no SignalR, no third-party libraries)
    /// - Always initiates OUTBOUND connections (NAT-safe)
    /// - Automatic reconnection with exponential backoff
    /// - Thread-safe outbound message queue
    /// - Heartbeat mechanism to detect dead connections
    /// - Clean shutdown via CancellationToken
    /// 
    /// USAGE:
    /// 1. Create configuration and instantiate client
    /// 2. Subscribe to events (MessageReceived, ConnectionStateChanged, etc.)
    /// 3. Call StartAsync() to begin connection
    /// 4. Use SendAsync() to queue outbound messages
    /// 5. Call StopAsync() for graceful shutdown
    /// 
    /// THREAD SAFETY:
    /// - SendAsync() is thread-safe and can be called from any thread
    /// - Events are raised on background threads - marshal to UI if needed
    /// </summary>
    public sealed class RelayWebSocketClient : IDisposable
    {
        private readonly RelayClientConfiguration _config;
        private readonly IRelayClientLogger _logger;
        private readonly ConcurrentQueue<string> _outboundQueue;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private Task? _sendTask;
        private Task? _heartbeatTask;

        private RelayConnectionState _state = RelayConnectionState.Disconnected;
        private readonly object _stateLock = new();
        private int _reconnectAttempts;
        private long _lastPingSentAt;
        private long _lastPongReceivedAt;
        private bool _isDisposed;

        // Buffer sizes for WebSocket operations
        private const int ReceiveBufferSize = 16384;
        private const int MaxMessageSize = 1048576; // 1 MB max message

        #region Events

        /// <summary>
        /// Raised when the connection state changes.
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>
        /// Raised when a message is received from the relay.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        /// <summary>
        /// Raised when an error occurs.
        /// </summary>
        public event EventHandler<RelayClientErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Raised when authentication completes (success or failure).
        /// </summary>
        public event EventHandler<AuthenticationResultEventArgs>? AuthenticationCompleted;

        /// <summary>
        /// Raised when a heartbeat pong is received.
        /// </summary>
        public event EventHandler<HeartbeatEventArgs>? HeartbeatReceived;

        #endregion

        #region Properties

        /// <summary>
        /// Current connection state.
        /// </summary>
        public RelayConnectionState State
        {
            get { lock (_stateLock) return _state; }
        }

        /// <summary>
        /// True if connected and authenticated.
        /// </summary>
        public bool IsConnected => State == RelayConnectionState.Connected;

        /// <summary>
        /// Number of messages waiting in the outbound queue.
        /// </summary>
        public int OutboundQueueCount => _outboundQueue.Count;

        /// <summary>
        /// Last round-trip time in milliseconds (-1 if unknown).
        /// </summary>
        public long LastRoundTripMs { get; private set; } = -1;

        #endregion

        /// <summary>
        /// Creates a new relay WebSocket client.
        /// </summary>
        /// <param name="config">Client configuration.</param>
        /// <param name="logger">Optional logger implementation.</param>
        public RelayWebSocketClient(RelayClientConfiguration config, IRelayClientLogger? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.Validate();
            _logger = logger ?? NullRelayClientLogger.Instance;
            _outboundQueue = new ConcurrentQueue<string>();
        }

        #region Public Methods

        /// <summary>
        /// Starts the client and begins connecting to the relay.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (State != RelayConnectionState.Disconnected)
            {
                throw new InvalidOperationException($"Cannot start client in state: {State}");
            }

            _logger.Info($"Starting relay client for station '{_config.StationId}'");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _reconnectAttempts = 0;

            await ConnectAsync();
        }

        /// <summary>
        /// Stops the client gracefully.
        /// </summary>
        public async Task StopAsync()
        {
            ThrowIfDisposed();

            if (State == RelayConnectionState.Disconnected || State == RelayConnectionState.Stopping)
            {
                return;
            }

            _logger.Info("Stopping relay client");
            SetState(RelayConnectionState.Stopping, "Client stop requested");

            // Signal cancellation
            _cts?.Cancel();

            // Close WebSocket gracefully
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client stopping", closeCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Error during WebSocket close: {ex.Message}");
                }
            }

            // Wait for tasks to complete
            var tasks = new[] { _receiveTask, _sendTask, _heartbeatTask };
            foreach (var task in tasks)
            {
                if (task != null)
                {
                    try
                    {
                        await task.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        _logger.Warning("Task did not complete within timeout");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }
            }

            CleanupWebSocket();
            SetState(RelayConnectionState.Disconnected, "Client stopped");
            _logger.Info("Relay client stopped");
        }

        /// <summary>
        /// Queues a message for sending to the relay.
        /// Thread-safe. Messages are queued if the connection is temporarily unavailable.
        /// </summary>
        /// <param name="envelope">The message envelope to send.</param>
        /// <returns>True if queued successfully, false if queue is full.</returns>
        public bool Send(MessageEnvelope envelope)
        {
            ThrowIfDisposed();

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            // Ensure stationId is set
            if (string.IsNullOrEmpty(envelope.StationId))
            {
                envelope.StationId = _config.StationId;
            }

            var json = envelope.ToJson();
            return EnqueueMessage(json);
        }

        /// <summary>
        /// Sends a message asynchronously, waiting for it to be transmitted.
        /// </summary>
        public async Task<bool> SendAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (!Send(envelope))
            {
                return false;
            }

            // Wait for the queue to drain (simple approach)
            // In production, you might want a more sophisticated ack mechanism
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (_outboundQueue.Count > 0 && DateTime.UtcNow < timeout)
            {
                await Task.Delay(10, cancellationToken);
            }

            return true;
        }

        /// <summary>
        /// Creates and sends a message with the specified type and payload.
        /// </summary>
        public bool Send<TPayload>(string messageType, TPayload payload, string? correlationId = null)
            where TPayload : class
        {
            var envelope = MessageEnvelope.Create(messageType, _config.StationId, payload, correlationId);
            return Send(envelope);
        }

        #endregion

        #region Connection Management

        private async Task ConnectAsync()
        {
            var token = _cts?.Token ?? CancellationToken.None;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    SetState(RelayConnectionState.Connecting, null);
                    _logger.Info($"Connecting to relay at {_config.RelayUrl}");

                    CleanupWebSocket();
                    _webSocket = new ClientWebSocket();

                    // Connect with timeout
                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(30));

                    await _webSocket.ConnectAsync(new Uri(_config.RelayUrl), connectCts.Token);

                    _logger.Info("WebSocket connected, authenticating...");
                    SetState(RelayConnectionState.Authenticating, null);

                    // Send authentication immediately after connect
                    await SendAuthenticationAsync(token);

                    // Start background tasks
                    _receiveTask = Task.Run(() => ReceiveLoopAsync(token), token);
                    _sendTask = Task.Run(() => SendLoopAsync(token), token);
                    _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(token), token);

                    // Reset reconnect counter on successful connect
                    _reconnectAttempts = 0;

                    // Wait for tasks to complete (they run until disconnection)
                    await Task.WhenAny(_receiveTask, _sendTask, _heartbeatTask);

                    // If we get here, one of the tasks completed (likely due to disconnection)
                    _logger.Warning("Connection loop ended");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    _logger.Info("Connection cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Connection error: {ex.Message}", ex);
                    RaiseError("Connection", ex, isRecoverable: true);
                }

                // Handle reconnection
                if (!token.IsCancellationRequested && _config.AutoReconnect)
                {
                    var delay = CalculateReconnectDelay();
                    _reconnectAttempts++;

                    if (_config.MaxReconnectAttempts >= 0 && _reconnectAttempts > _config.MaxReconnectAttempts)
                    {
                        _logger.Error($"Max reconnect attempts ({_config.MaxReconnectAttempts}) exceeded");
                        SetState(RelayConnectionState.Disconnected, "Max reconnect attempts exceeded");
                        break;
                    }

                    SetState(RelayConnectionState.Reconnecting, $"Reconnecting in {delay}ms (attempt {_reconnectAttempts})");
                    _logger.Info($"Reconnecting in {delay}ms (attempt {_reconnectAttempts})");

                    try
                    {
                        await Task.Delay(delay, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                else if (!_config.AutoReconnect)
                {
                    break;
                }
            }

            CleanupWebSocket();
            if (State != RelayConnectionState.Disconnected)
            {
                SetState(RelayConnectionState.Disconnected, "Connection loop ended");
            }
        }

        private int CalculateReconnectDelay()
        {
            // Exponential backoff with jitter
            var baseDelay = _config.InitialReconnectDelayMs * Math.Pow(_config.ReconnectBackoffMultiplier, _reconnectAttempts);
            var cappedDelay = Math.Min(baseDelay, _config.MaxReconnectDelayMs);

            // Add jitter (±20%)
            var jitter = cappedDelay * 0.2 * (Random.Shared.NextDouble() * 2 - 1);
            return (int)(cappedDelay + jitter);
        }

        private async Task SendAuthenticationAsync(CancellationToken cancellationToken)
        {
            var authPayload = new AuthenticatePayload
            {
                StationId = _config.StationId,
                StationToken = _config.StationToken,
                StationName = _config.StationName,
                ClientVersion = _config.ClientVersion
            };

            var envelope = MessageEnvelope.Create(
                MessageTypes.Authenticate,
                _config.StationId,
                authPayload);

            var json = envelope.ToJson();
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket!.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            _logger.Debug("Authentication message sent");
        }

        private void CleanupWebSocket()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                _webSocket = null;
            }
        }

        #endregion

        #region Background Loops

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuffer = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;

                    try
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    }
                    catch (WebSocketException ex)
                    {
                        _logger.Error($"WebSocket receive error: {ex.Message}", ex);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.Info($"Server initiated close: {result.CloseStatus} - {result.CloseStatusDescription}");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            var json = messageBuffer.ToString();
                            messageBuffer.Clear();

                            if (json.Length > MaxMessageSize)
                            {
                                _logger.Warning($"Message too large ({json.Length} bytes), discarding");
                                continue;
                            }

                            ProcessReceivedMessage(json);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.Error($"Receive loop error: {ex.Message}", ex);
                RaiseError("ReceiveLoop", ex, isRecoverable: true);
            }
        }

        private void ProcessReceivedMessage(string json)
        {
            try
            {
                var envelope = MessageEnvelope.FromJson(json);
                if (envelope == null)
                {
                    _logger.Warning($"Failed to parse message: {json.Substring(0, Math.Min(100, json.Length))}...");
                    return;
                }

                _logger.Debug($"Received message type: {envelope.Type}");

                // Handle system messages internally
                switch (envelope.Type)
                {
                    case MessageTypes.AuthResult:
                        HandleAuthResult(envelope);
                        break;

                    case MessageTypes.Pong:
                        HandlePong(envelope);
                        break;

                    case MessageTypes.Ping:
                        HandlePing(envelope);
                        break;

                    case MessageTypes.ServerShutdown:
                        _logger.Info("Server shutdown notification received");
                        break;

                    default:
                        // Raise event for application-level handling
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(envelope, json));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing message: {ex.Message}", ex);
            }
        }

        private void HandleAuthResult(MessageEnvelope envelope)
        {
            var result = envelope.GetPayload<AuthResultPayload>();
            if (result == null)
            {
                _logger.Warning("Invalid auth result payload");
                return;
            }

            if (result.Success)
            {
                _logger.Info($"Authentication successful: {result.Message}");
                SetState(RelayConnectionState.Connected, "Authenticated");
            }
            else
            {
                _logger.Error($"Authentication failed: {result.Message}");
                SetState(RelayConnectionState.Disconnected, $"Authentication failed: {result.Message}");
            }

            AuthenticationCompleted?.Invoke(this, new AuthenticationResultEventArgs(
                result.Success, result.Message, result.SessionToken));
        }

        private void HandlePong(MessageEnvelope envelope)
        {
            _lastPongReceivedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var payload = envelope.GetPayload<HeartbeatPayload>();
            if (payload != null && payload.SentAt > 0)
            {
                LastRoundTripMs = _lastPongReceivedAt - payload.SentAt;
                HeartbeatReceived?.Invoke(this, new HeartbeatEventArgs(LastRoundTripMs));
                _logger.Debug($"Heartbeat RTT: {LastRoundTripMs}ms");
            }
        }

        private void HandlePing(MessageEnvelope envelope)
        {
            // Respond to server pings immediately
            var pongEnvelope = MessageEnvelope.Create(
                MessageTypes.Pong,
                _config.StationId,
                envelope.GetPayload<HeartbeatPayload>() ?? new HeartbeatPayload());

            EnqueueMessage(pongEnvelope.ToJson(), highPriority: true);
        }

        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    if (_outboundQueue.TryDequeue(out var json))
                    {
                        await _sendLock.WaitAsync(cancellationToken);
                        try
                        {
                            var bytes = Encoding.UTF8.GetBytes(json);
                            await _webSocket.SendAsync(
                                new ArraySegment<byte>(bytes),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                cancellationToken);
                        }
                        finally
                        {
                            _sendLock.Release();
                        }
                    }
                    else
                    {
                        // Small delay to avoid busy-waiting
                        await Task.Delay(10, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.Error($"Send loop error: {ex.Message}", ex);
                RaiseError("SendLoop", ex, isRecoverable: true);
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            var heartbeatInterval = TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds);
            var heartbeatTimeout = TimeSpan.FromSeconds(_config.HeartbeatTimeoutSeconds);

            try
            {
                // Wait for connection to be established
                while (State != RelayConnectionState.Connected && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }

                while (!cancellationToken.IsCancellationRequested && State == RelayConnectionState.Connected)
                {
                    await Task.Delay(heartbeatInterval, cancellationToken);

                    // Check if we've received a pong recently
                    var timeSinceLastPong = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastPongReceivedAt;
                    if (_lastPingSentAt > 0 && _lastPongReceivedAt < _lastPingSentAt &&
                        timeSinceLastPong > heartbeatTimeout.TotalMilliseconds + heartbeatInterval.TotalMilliseconds)
                    {
                        _logger.Warning("Heartbeat timeout - connection appears dead");
                        break;
                    }

                    // Send ping
                    _lastPingSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var pingPayload = new HeartbeatPayload { SentAt = _lastPingSentAt };
                    var pingEnvelope = MessageEnvelope.Create(MessageTypes.Ping, _config.StationId, pingPayload);
                    EnqueueMessage(pingEnvelope.ToJson(), highPriority: true);

                    _logger.Debug("Heartbeat ping sent");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.Error($"Heartbeat loop error: {ex.Message}", ex);
            }
        }

        #endregion

        #region Helpers

        private bool EnqueueMessage(string json, bool highPriority = false)
        {
            // Check queue size limit
            if (_outboundQueue.Count >= _config.OutboundQueueSize)
            {
                _logger.Warning("Outbound queue full, message dropped");
                return false;
            }

            _outboundQueue.Enqueue(json);
            return true;
        }

        private void SetState(RelayConnectionState newState, string? reason)
        {
            RelayConnectionState previousState;
            lock (_stateLock)
            {
                if (_state == newState) return;
                previousState = _state;
                _state = newState;
            }

            _logger.Debug($"State change: {previousState} -> {newState} ({reason ?? "no reason"})");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(previousState, newState, reason));
        }

        private void RaiseError(string context, Exception ex, bool isRecoverable)
        {
            ErrorOccurred?.Invoke(this, new RelayClientErrorEventArgs(context, ex, isRecoverable));
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RelayWebSocketClient));
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _sendLock.Dispose();
            CleanupWebSocket();
        }

        #endregion
    }
}
