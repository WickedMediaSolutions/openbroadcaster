using System;

namespace OpenBroadcaster.Core.Models
{
    public sealed class TwitchSettings
    {
        public string UserName { get; set; } = string.Empty;
        public string OAuthToken { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string RadioStationName { get; set; } = string.Empty;
        public string PointsName { get; set; } = "Sheckles";
        public int RequestCost { get; set; } = 0;
        public int PlayNextCost { get; set; } = 50;
        public int SearchResultsLimit { get; set; } = 5;
        public int RequestCooldownSeconds { get; set; } = 60;
        public int ChatMessageAwardPoints { get; set; } = 1;
        public int IdleAwardPoints { get; set; } = 5;
        public int IdleAwardIntervalMinutes { get; set; } = 5;

        public TwitchSettings Clone()
        {
            return new TwitchSettings
            {
                UserName = UserName,
                OAuthToken = OAuthToken,
                Channel = Channel,
                RadioStationName = RadioStationName,
                PointsName = PointsName,
                RequestCost = RequestCost,
                PlayNextCost = PlayNextCost,
                SearchResultsLimit = SearchResultsLimit,
                RequestCooldownSeconds = RequestCooldownSeconds,
                ChatMessageAwardPoints = ChatMessageAwardPoints,
                IdleAwardPoints = IdleAwardPoints,
                IdleAwardIntervalMinutes = IdleAwardIntervalMinutes
            };
        }

        public TwitchChatOptions ToChatOptions()
        {
            return new TwitchChatOptions
            {
                UserName = UserName,
                OAuthToken = NormalizeToken(OAuthToken),
                Channel = Channel
            };
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var trimmed = token.Trim();
            
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
