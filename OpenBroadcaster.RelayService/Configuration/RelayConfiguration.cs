namespace OpenBroadcaster.RelayService.Configuration
{
    /// <summary>
    /// Configuration for the relay service.
    /// Loaded from appsettings.json under the "Relay" section.
    /// </summary>
    public sealed class RelayConfiguration
    {
        public const string SectionName = "Relay";

        /// <summary>
        /// JWT configuration for REST API authentication.
        /// </summary>
        public JwtConfiguration Jwt { get; set; } = new();

        /// <summary>
        /// Pre-configured stations with their authentication tokens.
        /// Key = StationId, Value = Station configuration.
        /// </summary>
        public Dictionary<string, StationConfiguration> Stations { get; set; } = new();

        /// <summary>
        /// API keys for WordPress/external clients.
        /// Key = API key string, Value = Key configuration.
        /// </summary>
        public Dictionary<string, ApiKeyConfiguration> ApiKeys { get; set; } = new();

        /// <summary>
        /// Maximum concurrent connections per station (1 = only one desktop app).
        /// </summary>
        public int MaxConnectionsPerStation { get; set; } = 1;

        /// <summary>
        /// Seconds without heartbeat before considering a station disconnected.
        /// </summary>
        public int HeartbeatTimeoutSeconds { get; set; } = 90;

        /// <summary>
        /// Maximum WebSocket message size in bytes.
        /// </summary>
        public int MaxMessageSizeBytes { get; set; } = 1048576;
    }

    public sealed class JwtConfiguration
    {
        /// <summary>
        /// Secret key for signing JWT tokens.
        /// MUST be at least 32 characters for HS256.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// JWT token issuer.
        /// </summary>
        public string Issuer { get; set; } = "OpenBroadcaster.RelayService";

        /// <summary>
        /// JWT token audience.
        /// </summary>
        public string Audience { get; set; } = "OpenBroadcaster";

        /// <summary>
        /// Token expiration in minutes.
        /// </summary>
        public int TokenExpirationMinutes { get; set; } = 60;
    }

    public sealed class StationConfiguration
    {
        /// <summary>
        /// Authentication token for this station.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable station name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Permissions granted to this station.
        /// </summary>
        public List<string> Permissions { get; set; } = new();
    }

    public sealed class ApiKeyConfiguration
    {
        /// <summary>
        /// Human-readable name for this API key.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Permissions granted by this API key.
        /// Values: "read", "search", "queue", "admin"
        /// </summary>
        public List<string> Permissions { get; set; } = new();
    }
}
