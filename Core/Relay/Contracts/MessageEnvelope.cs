using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts
{
    /// <summary>
    /// The root envelope for all messages in the OpenBroadcaster relay protocol.
    /// 
    /// DESIGN RATIONALE:
    /// - Envelope-based: All messages share a common outer structure for consistent parsing
    /// - Versioned: The 'version' field allows protocol evolution without breaking changes
    /// - Typed: The 'type' field enables strongly-typed deserialization
    /// - Station-aware: Every message identifies its origin/destination station
    /// - Forward-compatible: Unknown fields are ignored, unknown types can be logged/skipped
    /// 
    /// This contract is designed to remain stable for years. Changes should be additive only.
    /// </summary>
    public sealed class MessageEnvelope
    {
        /// <summary>
        /// Current protocol version. Increment for breaking changes.
        /// Format: major.minor (e.g., "1.0", "1.1", "2.0")
        /// </summary>
        public const string CurrentVersion = "1.0";

        /// <summary>
        /// The type of message contained in the payload.
        /// Used for routing and deserialization.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Protocol version for this message.
        /// Receivers should handle unknown versions gracefully.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = CurrentVersion;

        /// <summary>
        /// The station identifier this message pertains to.
        /// For outbound messages from desktop: the sending station's ID.
        /// For inbound messages to desktop: the target station's ID.
        /// </summary>
        [JsonPropertyName("stationId")]
        public string StationId { get; set; } = string.Empty;

        /// <summary>
        /// Optional correlation ID for request/response patterns.
        /// When a response is expected, the requestor generates a unique ID.
        /// The responder includes the same ID in the response.
        /// </summary>
        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        /// <summary>
        /// UTC timestamp when the message was created.
        /// ISO 8601 format for cross-platform compatibility.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// The message payload as a raw JSON element.
        /// Deserialized to specific types based on the 'type' field.
        /// </summary>
        [JsonPropertyName("payload")]
        public JsonElement? Payload { get; set; }

        /// <summary>
        /// Creates an envelope with the specified type and payload.
        /// </summary>
        public static MessageEnvelope Create<TPayload>(string type, string stationId, TPayload payload, string? correlationId = null)
            where TPayload : class
        {
            var payloadJson = JsonSerializer.SerializeToElement(payload, RelayJsonOptions.Default);

            return new MessageEnvelope
            {
                Type = type,
                Version = CurrentVersion,
                StationId = stationId,
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = payloadJson
            };
        }

        /// <summary>
        /// Deserializes the payload to the specified type.
        /// Returns null if the payload is null or deserialization fails.
        /// </summary>
        public TPayload? GetPayload<TPayload>() where TPayload : class
        {
            if (Payload == null || Payload.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            try
            {
                return Payload.Value.Deserialize<TPayload>(RelayJsonOptions.Default);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Serializes this envelope to JSON.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, RelayJsonOptions.Default);
        }

        /// <summary>
        /// Parses a JSON string into a MessageEnvelope.
        /// Returns null if parsing fails.
        /// </summary>
        public static MessageEnvelope? FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<MessageEnvelope>(json, RelayJsonOptions.Default);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
