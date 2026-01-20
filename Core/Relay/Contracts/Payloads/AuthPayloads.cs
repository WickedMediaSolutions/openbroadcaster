using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts.Payloads
{
    /// <summary>
    /// Authentication request sent from desktop app to relay immediately after WebSocket connect.
    /// 
    /// SECURITY NOTE:
    /// The stationToken is a pre-shared secret between the station and relay.
    /// In production, tokens should be:
    /// - Cryptographically random (minimum 32 bytes)
    /// - Stored securely (not in plain text config files)
    /// - Rotatable without service interruption
    /// </summary>
    public sealed class AuthenticatePayload
    {
        /// <summary>
        /// The unique identifier for this station.
        /// Typically a short, human-readable string like "WXYZ-FM" or a GUID.
        /// </summary>
        [JsonPropertyName("stationId")]
        public string StationId { get; set; } = string.Empty;

        /// <summary>
        /// The pre-shared secret token for this station.
        /// </summary>
        [JsonPropertyName("stationToken")]
        public string StationToken { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Human-readable station name for display.
        /// </summary>
        [JsonPropertyName("stationName")]
        public string? StationName { get; set; }

        /// <summary>
        /// Client version string for diagnostics and compatibility checking.
        /// Format: "OpenBroadcaster/1.0.0"
        /// </summary>
        [JsonPropertyName("clientVersion")]
        public string? ClientVersion { get; set; }
    }

    /// <summary>
    /// Authentication result sent from relay to desktop app.
    /// </summary>
    public sealed class AuthResultPayload
    {
        /// <summary>
        /// True if authentication was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Human-readable message describing the result.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// If successful, a session token for subsequent requests.
        /// This is optional - some implementations may not use session tokens.
        /// </summary>
        [JsonPropertyName("sessionToken")]
        public string? SessionToken { get; set; }

        /// <summary>
        /// Server-side timestamp for clock sync verification.
        /// </summary>
        [JsonPropertyName("serverTime")]
        public System.DateTimeOffset? ServerTime { get; set; }
    }
}
