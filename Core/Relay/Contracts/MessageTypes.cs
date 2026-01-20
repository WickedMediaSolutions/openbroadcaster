namespace OpenBroadcaster.Core.Relay.Contracts
{
    /// <summary>
    /// Well-known message types for the OpenBroadcaster relay protocol.
    /// 
    /// NAMING CONVENTION:
    /// - Use dot-separated namespaces: category.action
    /// - Categories: auth, now_playing, queue, library, system, request
    /// - Actions: update, search, add, remove, response, error
    /// 
    /// DESIGN RATIONALE:
    /// - String constants allow for extensibility without enum limitations
    /// - Dot notation provides natural grouping and filtering
    /// - All types are lowercase with underscores for consistency
    /// 
    /// When adding new message types:
    /// 1. Add the constant here with documentation
    /// 2. Create corresponding payload class in Payloads folder
    /// 3. Update message handlers in both client and relay
    /// </summary>
    public static class MessageTypes
    {
        // =====================================================================
        // AUTHENTICATION MESSAGES
        // =====================================================================

        /// <summary>
        /// Desktop → Relay: Initial authentication request after WebSocket connect.
        /// Payload: AuthenticatePayload
        /// </summary>
        public const string Authenticate = "auth.authenticate";

        /// <summary>
        /// Relay → Desktop: Authentication result.
        /// Payload: AuthResultPayload
        /// </summary>
        public const string AuthResult = "auth.result";

        // =====================================================================
        // SYSTEM/HEARTBEAT MESSAGES
        // =====================================================================

        /// <summary>
        /// Bidirectional: Heartbeat ping to maintain connection.
        /// Payload: HeartbeatPayload (optional)
        /// </summary>
        public const string Ping = "system.ping";

        /// <summary>
        /// Bidirectional: Heartbeat pong response.
        /// Payload: HeartbeatPayload (optional)
        /// </summary>
        public const string Pong = "system.pong";

        /// <summary>
        /// Relay → Desktop: Server is shutting down gracefully.
        /// Payload: null
        /// </summary>
        public const string ServerShutdown = "system.shutdown";

        /// <summary>
        /// Any → Any: Error response for failed operations.
        /// Payload: ErrorPayload
        /// </summary>
        public const string Error = "system.error";

        // =====================================================================
        // NOW PLAYING MESSAGES
        // =====================================================================

        /// <summary>
        /// Desktop → Relay: Current track has changed.
        /// Payload: NowPlayingPayload
        /// </summary>
        public const string NowPlayingUpdate = "now_playing.update";

        /// <summary>
        /// REST → Relay → Desktop: Request current now playing state.
        /// Payload: null (correlationId required for response routing)
        /// </summary>
        public const string NowPlayingRequest = "now_playing.request";

        /// <summary>
        /// Desktop → Relay → REST: Response to now playing request.
        /// Payload: NowPlayingPayload
        /// </summary>
        public const string NowPlayingResponse = "now_playing.response";

        // =====================================================================
        // QUEUE MESSAGES
        // =====================================================================

        /// <summary>
        /// Desktop → Relay: Queue state has changed.
        /// Payload: QueueStatePayload
        /// </summary>
        public const string QueueUpdate = "queue.update";

        /// <summary>
        /// REST → Relay → Desktop: Request current queue state.
        /// Payload: null (correlationId required for response routing)
        /// </summary>
        public const string QueueRequest = "queue.request";

        /// <summary>
        /// Desktop → Relay → REST: Response to queue request.
        /// Payload: QueueStatePayload
        /// </summary>
        public const string QueueResponse = "queue.response";

        /// <summary>
        /// REST → Relay → Desktop: Add track to queue.
        /// Payload: QueueAddPayload
        /// </summary>
        public const string QueueAdd = "queue.add";

        /// <summary>
        /// Desktop → Relay → REST: Result of queue add operation.
        /// Payload: QueueAddResultPayload
        /// </summary>
        public const string QueueAddResult = "queue.add_result";

        /// <summary>
        /// REST → Relay → Desktop: Remove track from queue by position.
        /// Payload: QueueRemovePayload
        /// </summary>
        public const string QueueRemove = "queue.remove";

        /// <summary>
        /// REST → Relay → Desktop: Clear entire queue.
        /// Payload: null
        /// </summary>
        public const string QueueClear = "queue.clear";

        /// <summary>
        /// REST → Relay → Desktop: Skip current track.
        /// Payload: null
        /// </summary>
        public const string QueueSkip = "queue.skip";

        // =====================================================================
        // LIBRARY SEARCH MESSAGES
        // =====================================================================

        /// <summary>
        /// REST → Relay → Desktop: Search library for tracks.
        /// Payload: LibrarySearchPayload
        /// </summary>
        public const string LibrarySearch = "library.search";

        /// <summary>
        /// Desktop → Relay → REST: Library search results.
        /// Payload: LibrarySearchResultPayload
        /// </summary>
        public const string LibrarySearchResult = "library.search_result";

        // =====================================================================
        // REQUEST MESSAGES (for request line / song requests)
        // =====================================================================

        /// <summary>
        /// REST → Relay → Desktop: Song request from listener.
        /// Payload: SongRequestPayload
        /// </summary>
        public const string SongRequest = "request.song";

        /// <summary>
        /// Desktop → Relay → REST: Song request result.
        /// Payload: SongRequestResultPayload
        /// </summary>
        public const string SongRequestResult = "request.song_result";
    }
}
