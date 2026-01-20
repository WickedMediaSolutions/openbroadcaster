using System;
using OpenBroadcaster.Core.Relay.Contracts;

namespace OpenBroadcaster.Core.Relay.Client
{
    /// <summary>
    /// Connection state for the relay client.
    /// </summary>
    public enum RelayConnectionState
    {
        /// <summary>Client has not been started or is stopped.</summary>
        Disconnected,

        /// <summary>Client is attempting to connect.</summary>
        Connecting,

        /// <summary>WebSocket is connected, awaiting authentication.</summary>
        Authenticating,

        /// <summary>Fully connected and authenticated.</summary>
        Connected,

        /// <summary>Connection lost, waiting to reconnect.</summary>
        Reconnecting,

        /// <summary>Client is shutting down.</summary>
        Stopping
    }

    /// <summary>
    /// Event arguments for connection state changes.
    /// </summary>
    public sealed class ConnectionStateChangedEventArgs : EventArgs
    {
        public RelayConnectionState PreviousState { get; }
        public RelayConnectionState CurrentState { get; }
        public string? Reason { get; }

        public ConnectionStateChangedEventArgs(RelayConnectionState previousState, RelayConnectionState currentState, string? reason = null)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for received messages.
    /// </summary>
    public sealed class MessageReceivedEventArgs : EventArgs
    {
        public MessageEnvelope Envelope { get; }
        public string RawJson { get; }

        public MessageReceivedEventArgs(MessageEnvelope envelope, string rawJson)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            RawJson = rawJson ?? throw new ArgumentNullException(nameof(rawJson));
        }
    }

    /// <summary>
    /// Event arguments for client errors.
    /// </summary>
    public sealed class RelayClientErrorEventArgs : EventArgs
    {
        public string Context { get; }
        public Exception Exception { get; }
        public bool IsRecoverable { get; }

        public RelayClientErrorEventArgs(string context, Exception exception, bool isRecoverable)
        {
            Context = context ?? string.Empty;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            IsRecoverable = isRecoverable;
        }
    }

    /// <summary>
    /// Event arguments for authentication results.
    /// </summary>
    public sealed class AuthenticationResultEventArgs : EventArgs
    {
        public bool Success { get; }
        public string? Message { get; }
        public string? SessionToken { get; }

        public AuthenticationResultEventArgs(bool success, string? message, string? sessionToken)
        {
            Success = success;
            Message = message;
            SessionToken = sessionToken;
        }
    }

    /// <summary>
    /// Event arguments for heartbeat events.
    /// </summary>
    public sealed class HeartbeatEventArgs : EventArgs
    {
        /// <summary>
        /// Round-trip time in milliseconds (-1 if unknown).
        /// </summary>
        public long RoundTripMs { get; }

        public HeartbeatEventArgs(long roundTripMs)
        {
            RoundTripMs = roundTripMs;
        }
    }
}
