using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Relay.Contracts
{
    /// <summary>
    /// Centralized JSON serialization options for the relay protocol.
    /// 
    /// DESIGN RATIONALE:
    /// - Consistent serialization across all components (desktop, relay, WordPress)
    /// - Case-insensitive property matching for flexibility
    /// - Camel case output for JavaScript/PHP compatibility
    /// - Lenient parsing to handle minor schema variations
    /// </summary>
    public static class RelayJsonOptions
    {
        /// <summary>
        /// Default JSON options for all relay protocol serialization.
        /// </summary>
        public static readonly JsonSerializerOptions Default = new()
        {
            // Use camelCase for property names (JavaScript/PHP convention)
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            // Be lenient when deserializing (accept PascalCase, camelCase, etc.)
            PropertyNameCaseInsensitive = true,

            // Include fields with default values for explicit schema
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Allow trailing commas for hand-edited JSON
            AllowTrailingCommas = true,

            // Allow comments in JSON for configuration files
            ReadCommentHandling = JsonCommentHandling.Skip,

            // Write indented JSON for debugging (can be disabled in production)
            WriteIndented = false,

            // Use string enums for readability
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// Pretty-printed JSON options for logging and debugging.
        /// </summary>
        public static readonly JsonSerializerOptions Indented = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }
}
