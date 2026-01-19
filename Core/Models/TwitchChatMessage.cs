using System;

namespace OpenBroadcaster.Core.Models
{
    public sealed class TwitchChatMessage
    {
        public TwitchChatMessage(
            string userName,
            string message,
            DateTime timestampUtc,
            bool isSystem = false,
            bool isFromBroadcaster = false,
            bool isFromBot = false)
        {
            UserName = userName ?? string.Empty;
            Message = message ?? string.Empty;
            TimestampUtc = timestampUtc;
            IsSystem = isSystem;
            IsFromBroadcaster = isFromBroadcaster;
            IsFromBot = isFromBot;
        }

        public string UserName { get; }
        public string Message { get; }
        public DateTime TimestampUtc { get; }
        public bool IsSystem { get; }
        public bool IsFromBroadcaster { get; }
        public bool IsFromBot { get; }
    }
}
