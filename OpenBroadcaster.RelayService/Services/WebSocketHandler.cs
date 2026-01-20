using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Options;
using OpenBroadcaster.RelayService.Configuration;
using OpenBroadcaster.RelayService.Contracts;
using OpenBroadcaster.RelayService.Contracts.Payloads;

namespace OpenBroadcaster.RelayService.Services
{
    /// <summary>
    /// Handles WebSocket connections from OpenBroadcaster desktop applications.
    /// 
    /// DESIGN RATIONALE:
    /// - One handler instance per connection (middleware creates new handler per request)
    /// - Authenticates stations immediately after connect
    /// - Routes messages to appropriate handlers
    /// - Updates cached state for fast REST queries
    /// </summary>
    public sealed class WebSocketHandler
    {
        private readonly StationConnectionManager _connectionManager;
        private readonly RelayConfiguration _config;
        private readonly ILogger<WebSocketHandler> _logger;

        private const int ReceiveBufferSize = 16384;
        private const int MaxMessageSize = 1048576;

        public WebSocketHandler(
            StationConnectionManager connectionManager,
            IOptions<RelayConfiguration> config,
            ILogger<WebSocketHandler> logger)
        {
            _connectionManager = connectionManager;
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Handles a new WebSocket connection.
        /// </summary>
        public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            StationConnection? connection = null;

            try
            {
                // First message must be authentication
                var authMessage = await ReceiveMessageAsync(webSocket, cancellationToken);
                if (authMessage == null || authMessage.Type != MessageTypes.Authenticate)
                {
                    _logger.LogWarning("First message was not authentication");
                    await SendErrorAndCloseAsync(webSocket, ErrorCodes.AuthRequired, "Authentication required", cancellationToken);
                    return;
                }

                // Validate authentication
                var authPayload = authMessage.GetPayload<AuthenticatePayload>();
                if (authPayload == null)
                {
                    await SendErrorAndCloseAsync(webSocket, ErrorCodes.InvalidPayload, "Invalid authentication payload", cancellationToken);
                    return;
                }

                var authResult = ValidateStationAuth(authPayload);
                if (!authResult.Success)
                {
                    _logger.LogWarning("Authentication failed for station '{StationId}': {Reason}",
                        authPayload.StationId, authResult.Message);
                    
                    await SendAuthResultAsync(webSocket, authResult, authPayload.StationId, cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Authentication failed", cancellationToken);
                    return;
                }

                // Create and register connection
                connection = new StationConnection(
                    authPayload.StationId,
                    authPayload.StationName ?? authPayload.StationId,
                    webSocket)
                {
                    ClientVersion = authPayload.ClientVersion,
                    IsAuthenticated = true
                };

                if (!_connectionManager.RegisterConnection(connection))
                {
                    await SendAuthResultAsync(webSocket, new AuthResultPayload
                    {
                        Success = false,
                        Message = "Station already connected"
                    }, authPayload.StationId, cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Station already connected", cancellationToken);
                    return;
                }

                // Send success response
                await SendAuthResultAsync(webSocket, authResult, authPayload.StationId, cancellationToken);

                _logger.LogInformation("Station '{StationId}' authenticated successfully (version: {Version})",
                    authPayload.StationId, authPayload.ClientVersion);

                // Main receive loop
                await ReceiveLoopAsync(connection, cancellationToken);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("WebSocket connection closed prematurely");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket connection cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket handler");
            }
            finally
            {
                if (connection != null)
                {
                    _connectionManager.RemoveConnection(connection.StationId);
                }

                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", closeCts.Token);
                    }
                    catch
                    {
                        // Ignore close errors
                    }
                }
            }
        }

        private AuthResultPayload ValidateStationAuth(AuthenticatePayload auth)
        {
            if (string.IsNullOrWhiteSpace(auth.StationId))
            {
                return new AuthResultPayload
                {
                    Success = false,
                    Message = "Station ID is required"
                };
            }

            // Check if station is configured
            if (!_config.Stations.TryGetValue(auth.StationId, out var stationConfig))
            {
                return new AuthResultPayload
                {
                    Success = false,
                    Message = "Unknown station"
                };
            }

            // Validate token
            if (string.IsNullOrWhiteSpace(auth.StationToken) || auth.StationToken != stationConfig.Token)
            {
                return new AuthResultPayload
                {
                    Success = false,
                    Message = "Invalid station token"
                };
            }

            // Check if already connected
            if (_connectionManager.IsStationConnected(auth.StationId))
            {
                return new AuthResultPayload
                {
                    Success = false,
                    Message = "Station is already connected"
                };
            }

            return new AuthResultPayload
            {
                Success = true,
                Message = "Authentication successful",
                ServerTime = DateTimeOffset.UtcNow
            };
        }

        private async Task ReceiveLoopAsync(StationConnection connection, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && connection.IsConnected)
            {
                var message = await ReceiveMessageAsync(connection.WebSocket, cancellationToken);
                if (message == null)
                {
                    break;
                }

                await HandleMessageAsync(connection, message, cancellationToken);
            }
        }

        private async Task HandleMessageAsync(StationConnection connection, MessageEnvelope message, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Received message type '{Type}' from station '{StationId}'",
                message.Type, connection.StationId);

            switch (message.Type)
            {
                case MessageTypes.Ping:
                    await HandlePingAsync(connection, message, cancellationToken);
                    break;

                case MessageTypes.Pong:
                    _connectionManager.UpdateHeartbeat(connection.StationId);
                    break;

                case MessageTypes.NowPlayingUpdate:
                    HandleNowPlayingUpdate(connection, message);
                    break;

                case MessageTypes.QueueUpdate:
                    HandleQueueUpdate(connection, message);
                    break;

                case MessageTypes.NowPlayingResponse:
                case MessageTypes.QueueResponse:
                case MessageTypes.QueueAddResult:
                case MessageTypes.LibrarySearchResult:
                case MessageTypes.SongRequestResult:
                    // These are responses - complete pending requests
                    _connectionManager.CompleteRequest(connection.StationId, message);
                    break;

                case MessageTypes.Error:
                    HandleError(connection, message);
                    break;

                default:
                    _logger.LogDebug("Unhandled message type: {Type}", message.Type);
                    break;
            }
        }

        private async Task HandlePingAsync(StationConnection connection, MessageEnvelope message, CancellationToken cancellationToken)
        {
            _connectionManager.UpdateHeartbeat(connection.StationId);

            // Respond with pong
            var pong = MessageEnvelope.Create(
                MessageTypes.Pong,
                connection.StationId,
                message.GetPayload<HeartbeatPayload>() ?? new HeartbeatPayload());

            await _connectionManager.SendMessageAsync(connection, pong, cancellationToken);
        }

        private void HandleNowPlayingUpdate(StationConnection connection, MessageEnvelope message)
        {
            var payload = message.GetPayload<NowPlayingPayload>();
            if (payload != null)
            {
                _connectionManager.UpdateNowPlaying(connection.StationId, payload);
            }
        }

        private void HandleQueueUpdate(StationConnection connection, MessageEnvelope message)
        {
            var payload = message.GetPayload<QueueStatePayload>();
            if (payload != null)
            {
                _connectionManager.UpdateQueueState(connection.StationId, payload);
            }
        }

        private void HandleError(StationConnection connection, MessageEnvelope message)
        {
            var error = message.GetPayload<ErrorPayload>();
            if (error != null)
            {
                _logger.LogWarning("Error from station '{StationId}': [{Code}] {Message}",
                    connection.StationId, error.Code, error.Message);
            }

            // Complete any pending request with this correlation ID
            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                _connectionManager.CompleteRequest(connection.StationId, message);
            }
        }

        private async Task<MessageEnvelope?> ReceiveMessageAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuilder = new StringBuilder();

            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (WebSocketException)
                {
                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (messageBuilder.Length > MaxMessageSize)
                    {
                        _logger.LogWarning("Message too large, discarding");
                        return null;
                    }

                    if (result.EndOfMessage)
                    {
                        var json = messageBuilder.ToString();
                        return MessageEnvelope.FromJson(json);
                    }
                }
            }
        }

        private async Task SendAuthResultAsync(WebSocket webSocket, AuthResultPayload result, string stationId, CancellationToken cancellationToken)
        {
            var message = MessageEnvelope.Create(MessageTypes.AuthResult, stationId, result);
            var json = message.ToJson();
            var bytes = Encoding.UTF8.GetBytes(json);

            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }

        private async Task SendErrorAndCloseAsync(WebSocket webSocket, string errorCode, string message, CancellationToken cancellationToken)
        {
            var error = new ErrorPayload { Code = errorCode, Message = message };
            var envelope = MessageEnvelope.Create(MessageTypes.Error, "", error);
            var json = envelope.ToJson();
            var bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);

                await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, message, cancellationToken);
            }
            catch
            {
                // Ignore send/close errors during error handling
            }
        }
    }
}
