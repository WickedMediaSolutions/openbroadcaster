using System;
using System.Threading;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Relay.Contracts;
using OpenBroadcaster.Core.Relay.Contracts.Payloads;

namespace OpenBroadcaster.Core.Relay.Client
{
    /// <summary>
    /// Handles incoming messages from the relay and dispatches them to appropriate handlers.
    /// 
    /// DESIGN RATIONALE:
    /// - Strongly-typed message handling via generic delegates
    /// - Clean separation from WebSocket transport concerns
    /// - Async handler support for I/O-bound operations
    /// - Correlation ID tracking for request/response patterns
    /// </summary>
    public sealed class RelayMessageHandler
    {
        private readonly RelayWebSocketClient _client;
        private readonly IRelayClientLogger _logger;

        #region Message Handlers

        /// <summary>
        /// Handler for now playing requests from the relay.
        /// Return the current now playing info.
        /// </summary>
        public Func<Task<NowPlayingPayload>>? OnNowPlayingRequest { get; set; }

        /// <summary>
        /// Handler for queue state requests.
        /// Return the current queue state.
        /// </summary>
        public Func<Task<QueueStatePayload>>? OnQueueStateRequest { get; set; }

        /// <summary>
        /// Handler for queue add commands.
        /// Return the result of the add operation.
        /// </summary>
        public Func<QueueAddPayload, Task<QueueAddResultPayload>>? OnQueueAdd { get; set; }

        /// <summary>
        /// Handler for queue remove commands.
        /// </summary>
        public Func<QueueRemovePayload, Task<bool>>? OnQueueRemove { get; set; }

        /// <summary>
        /// Handler for queue clear commands.
        /// </summary>
        public Func<Task<bool>>? OnQueueClear { get; set; }

        /// <summary>
        /// Handler for skip commands.
        /// </summary>
        public Func<Task<bool>>? OnSkip { get; set; }

        /// <summary>
        /// Handler for library search requests.
        /// </summary>
        public Func<LibrarySearchPayload, Task<LibrarySearchResultPayload>>? OnLibrarySearch { get; set; }

        /// <summary>
        /// Handler for song requests.
        /// </summary>
        public Func<SongRequestPayload, Task<SongRequestResultPayload>>? OnSongRequest { get; set; }

        #endregion

        public RelayMessageHandler(RelayWebSocketClient client, IRelayClientLogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? NullRelayClientLogger.Instance;

            // Subscribe to message events
            _client.MessageReceived += OnMessageReceived;
        }

        private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            try
            {
                await HandleMessageAsync(e.Envelope);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error handling message type '{e.Envelope.Type}': {ex.Message}", ex);
            }
        }

        private async Task HandleMessageAsync(MessageEnvelope envelope)
        {
            _logger.Debug($"Handling message type: {envelope.Type}");

            switch (envelope.Type)
            {
                case MessageTypes.NowPlayingRequest:
                    await HandleNowPlayingRequestAsync(envelope);
                    break;

                case MessageTypes.QueueRequest:
                    await HandleQueueRequestAsync(envelope);
                    break;

                case MessageTypes.QueueAdd:
                    await HandleQueueAddAsync(envelope);
                    break;

                case MessageTypes.QueueRemove:
                    await HandleQueueRemoveAsync(envelope);
                    break;

                case MessageTypes.QueueClear:
                    await HandleQueueClearAsync(envelope);
                    break;

                case MessageTypes.QueueSkip:
                    await HandleSkipAsync(envelope);
                    break;

                case MessageTypes.LibrarySearch:
                    await HandleLibrarySearchAsync(envelope);
                    break;

                case MessageTypes.SongRequest:
                    await HandleSongRequestAsync(envelope);
                    break;

                default:
                    _logger.Debug($"Unhandled message type: {envelope.Type}");
                    break;
            }
        }

        private async Task HandleNowPlayingRequestAsync(MessageEnvelope request)
        {
            if (OnNowPlayingRequest == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Now playing not available");
                return;
            }

            var payload = await OnNowPlayingRequest();
            var response = MessageEnvelope.Create(
                MessageTypes.NowPlayingResponse,
                request.StationId,
                payload,
                request.CorrelationId);
            _client.Send(response);
        }

        private async Task HandleQueueRequestAsync(MessageEnvelope request)
        {
            if (OnQueueStateRequest == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Queue not available");
                return;
            }

            var payload = await OnQueueStateRequest();
            var response = MessageEnvelope.Create(
                MessageTypes.QueueResponse,
                request.StationId,
                payload,
                request.CorrelationId);
            _client.Send(response);
        }

        private async Task HandleQueueAddAsync(MessageEnvelope request)
        {
            if (OnQueueAdd == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Queue add not available");
                return;
            }

            var addPayload = request.GetPayload<QueueAddPayload>();
            if (addPayload == null)
            {
                SendError(request, ErrorCodes.InvalidPayload, "Invalid queue add payload");
                return;
            }

            var result = await OnQueueAdd(addPayload);
            var response = MessageEnvelope.Create(
                MessageTypes.QueueAddResult,
                request.StationId,
                result,
                request.CorrelationId);
            _client.Send(response);
        }

        private async Task HandleQueueRemoveAsync(MessageEnvelope request)
        {
            if (OnQueueRemove == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Queue remove not available");
                return;
            }

            var removePayload = request.GetPayload<QueueRemovePayload>();
            if (removePayload == null)
            {
                SendError(request, ErrorCodes.InvalidPayload, "Invalid queue remove payload");
                return;
            }

            var success = await OnQueueRemove(removePayload);
            // Queue remove doesn't need a response, but we could send one
        }

        private async Task HandleQueueClearAsync(MessageEnvelope request)
        {
            if (OnQueueClear == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Queue clear not available");
                return;
            }

            await OnQueueClear();
        }

        private async Task HandleSkipAsync(MessageEnvelope request)
        {
            if (OnSkip == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Skip not available");
                return;
            }

            await OnSkip();
        }

        private async Task HandleLibrarySearchAsync(MessageEnvelope request)
        {
            if (OnLibrarySearch == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Library search not available");
                return;
            }

            var searchPayload = request.GetPayload<LibrarySearchPayload>();
            if (searchPayload == null)
            {
                SendError(request, ErrorCodes.InvalidPayload, "Invalid search payload");
                return;
            }

            var result = await OnLibrarySearch(searchPayload);
            var response = MessageEnvelope.Create(
                MessageTypes.LibrarySearchResult,
                request.StationId,
                result,
                request.CorrelationId);
            _client.Send(response);
        }

        private async Task HandleSongRequestAsync(MessageEnvelope request)
        {
            if (OnSongRequest == null)
            {
                SendError(request, ErrorCodes.NotImplemented, "Song requests not available");
                return;
            }

            var songRequest = request.GetPayload<SongRequestPayload>();
            if (songRequest == null)
            {
                SendError(request, ErrorCodes.InvalidPayload, "Invalid song request payload");
                return;
            }

            var result = await OnSongRequest(songRequest);
            var response = MessageEnvelope.Create(
                MessageTypes.SongRequestResult,
                request.StationId,
                result,
                request.CorrelationId);
            _client.Send(response);
        }

        private void SendError(MessageEnvelope request, string errorCode, string message)
        {
            var errorPayload = new ErrorPayload
            {
                Code = errorCode,
                Message = message,
                SourceType = request.Type
            };

            var response = MessageEnvelope.Create(
                MessageTypes.Error,
                request.StationId,
                errorPayload,
                request.CorrelationId);
            _client.Send(response);
        }

        /// <summary>
        /// Sends a now playing update to the relay.
        /// </summary>
        public void SendNowPlayingUpdate(NowPlayingPayload payload)
        {
            _client.Send(MessageTypes.NowPlayingUpdate, payload);
        }

        /// <summary>
        /// Sends a queue state update to the relay.
        /// </summary>
        public void SendQueueUpdate(QueueStatePayload payload)
        {
            _client.Send(MessageTypes.QueueUpdate, payload);
        }

        /// <summary>
        /// Unsubscribes from client events.
        /// </summary>
        public void Detach()
        {
            _client.MessageReceived -= OnMessageReceived;
        }
    }
}
