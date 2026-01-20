using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.RelayService.Contracts
{
    /// <summary>
    /// The root envelope for all messages in the OpenBroadcaster relay protocol.
    /// This is a copy of the contract from the desktop app - in production, use a shared library.
    /// </summary>
    public sealed class MessageEnvelope
    {
        public const string CurrentVersion = "1.0";

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = CurrentVersion;

        [JsonPropertyName("stationId")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("payload")]
        public JsonElement? Payload { get; set; }

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

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, RelayJsonOptions.Default);
        }

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

    public static class RelayJsonOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }
}
