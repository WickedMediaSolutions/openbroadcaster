using System;

namespace OpenBroadcaster.Core.Models
{
    public sealed class TwitchChatOptions
    {
        public string UserName { get; init; } = string.Empty;
        public string OAuthToken { get; init; } = string.Empty;
        public string Channel { get; init; } = string.Empty;

        public string NormalizedChannel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Channel))
                {
                    return string.Empty;
                }

                var trimmed = Channel.Trim();
                return trimmed.StartsWith("#", StringComparison.Ordinal) ? trimmed[1..] : trimmed;
            }
        }

        public string ChannelWithHash => string.IsNullOrWhiteSpace(NormalizedChannel)
            ? string.Empty
            : $"#{NormalizedChannel.ToLowerInvariant()}";

        public bool IsValid => !string.IsNullOrWhiteSpace(UserName)
            && !string.IsNullOrWhiteSpace(OAuthToken)
            && !string.IsNullOrWhiteSpace(NormalizedChannel);

        public static TwitchChatOptions FromEnvironment()
        {
            var user = Environment.GetEnvironmentVariable("TWITCH_USERNAME") ?? string.Empty;
            var token = Environment.GetEnvironmentVariable("TWITCH_OAUTH_TOKEN") ?? string.Empty;
            var channel = Environment.GetEnvironmentVariable("TWITCH_CHANNEL") ?? string.Empty;

            return new TwitchChatOptions
            {
                UserName = user.Trim(),
                OAuthToken = NormalizeToken(token),
                Channel = channel.Trim()
            };
        }

        private static string NormalizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            
            // Strip existing oauth: prefix (any case) and re-add as lowercase
            if (trimmed.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(6);
            }
            
            // Twitch requires exactly lowercase "oauth:"
            return $"oauth:{trimmed}";
        }
    }
}
