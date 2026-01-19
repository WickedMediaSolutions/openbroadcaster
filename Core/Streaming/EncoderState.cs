using System;

namespace OpenBroadcaster.Core.Streaming
{
    public enum EncoderState
    {
        Stopped = 0,
        Connecting = 1,
        Streaming = 2,
        Failed = 3
    }

    public sealed class EncoderStatus
    {
        public EncoderStatus(Guid profileId, string name, EncoderState state, string message, DateTimeOffset? lastConnected)
        {
            ProfileId = profileId;
            Name = name;
            State = state;
            Message = message;
            LastConnected = lastConnected;
        }

        public Guid ProfileId { get; }
        public string Name { get; }
        public EncoderState State { get; }
        public string Message { get; }
        public DateTimeOffset? LastConnected { get; }
    }

    public sealed class EncoderStatusChangedEventArgs : EventArgs
    {
        public EncoderStatusChangedEventArgs(EncoderStatus status)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
        }

        public EncoderStatus Status { get; }
    }
}
