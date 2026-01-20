namespace OpenBroadcaster.RelayService.Contracts
{
    /// <summary>
    /// Well-known message types for the OpenBroadcaster relay protocol.
    /// Mirrored from desktop app contracts.
    /// </summary>
    public static class MessageTypes
    {
        // Authentication
        public const string Authenticate = "auth.authenticate";
        public const string AuthResult = "auth.result";

        // System
        public const string Ping = "system.ping";
        public const string Pong = "system.pong";
        public const string ServerShutdown = "system.shutdown";
        public const string Error = "system.error";

        // Now Playing
        public const string NowPlayingUpdate = "now_playing.update";
        public const string NowPlayingRequest = "now_playing.request";
        public const string NowPlayingResponse = "now_playing.response";

        // Queue
        public const string QueueUpdate = "queue.update";
        public const string QueueRequest = "queue.request";
        public const string QueueResponse = "queue.response";
        public const string QueueAdd = "queue.add";
        public const string QueueAddResult = "queue.add_result";
        public const string QueueRemove = "queue.remove";
        public const string QueueClear = "queue.clear";
        public const string QueueSkip = "queue.skip";

        // Library
        public const string LibrarySearch = "library.search";
        public const string LibrarySearchResult = "library.search_result";

        // Requests
        public const string SongRequest = "request.song";
        public const string SongRequestResult = "request.song_result";
    }
}
