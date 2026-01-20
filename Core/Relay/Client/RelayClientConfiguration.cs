using System;

namespace OpenBroadcaster.Core.Relay.Client
{
    /// <summary>
    /// Configuration for the relay WebSocket client.
    /// </summary>
    public sealed class RelayClientConfiguration
    {
        /// <summary>
        /// The WebSocket URL of the relay service.
        /// Example: "wss://relay.openbroadcaster.com/ws"
        /// </summary>
        public string RelayUrl { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier for this station.
        /// </summary>
        public string StationId { get; set; } = string.Empty;

        /// <summary>
        /// The authentication token for this station.
        /// </summary>
        public string StationToken { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable station name (optional).
        /// </summary>
        public string? StationName { get; set; }

        /// <summary>
        /// Interval between heartbeat pings in seconds.
        /// Default: 30 seconds as specified.
        /// </summary>
        public int HeartbeatIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Timeout for heartbeat response in seconds.
        /// If no pong received within this time, connection is considered dead.
        /// </summary>
        public int HeartbeatTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Initial reconnect delay in milliseconds.
        /// </summary>
        public int InitialReconnectDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum reconnect delay in milliseconds (for exponential backoff cap).
        /// </summary>
        public int MaxReconnectDelayMs { get; set; } = 60000;

        /// <summary>
        /// Multiplier for exponential backoff.
        /// Each failed reconnect multiplies the delay by this factor.
        /// </summary>
        public double ReconnectBackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Maximum number of reconnect attempts before giving up.
        /// Set to -1 for infinite retries (recommended for production).
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = -1;

        /// <summary>
        /// Size of the outbound message queue.
        /// Messages are queued when the connection is temporarily unavailable.
        /// </summary>
        public int OutboundQueueSize { get; set; } = 1000;

        /// <summary>
        /// Whether to auto-reconnect on connection loss.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Client version string sent during authentication.
        /// </summary>
        public string ClientVersion { get; set; } = "OpenBroadcaster/1.0.0";

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(RelayUrl))
            {
                throw new InvalidOperationException("RelayUrl is required.");
            }

            if (!Uri.TryCreate(RelayUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                throw new InvalidOperationException("RelayUrl must be a valid WebSocket URL (ws:// or wss://).");
            }

            if (string.IsNullOrWhiteSpace(StationId))
            {
                throw new InvalidOperationException("StationId is required.");
            }

            if (string.IsNullOrWhiteSpace(StationToken))
            {
                throw new InvalidOperationException("StationToken is required.");
            }

            if (HeartbeatIntervalSeconds < 5)
            {
                throw new InvalidOperationException("HeartbeatIntervalSeconds must be at least 5.");
            }

            if (HeartbeatTimeoutSeconds < 1 || HeartbeatTimeoutSeconds >= HeartbeatIntervalSeconds)
            {
                throw new InvalidOperationException("HeartbeatTimeoutSeconds must be between 1 and HeartbeatIntervalSeconds.");
            }
        }
    }
}
