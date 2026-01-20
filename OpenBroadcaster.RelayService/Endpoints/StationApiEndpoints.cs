using Microsoft.AspNetCore.Mvc;
using OpenBroadcaster.RelayService.Contracts;
using OpenBroadcaster.RelayService.Contracts.Payloads;
using OpenBroadcaster.RelayService.Services;

namespace OpenBroadcaster.RelayService.Endpoints
{
    /// <summary>
    /// REST API endpoints for WordPress and external clients.
    /// 
    /// PERMISSION MODEL:
    /// - Public (read): Get now playing, get queue state
    /// - DJ (search, queue): Search library, add to queue
    /// - Admin: All operations including clear queue, skip
    /// 
    /// AUTHENTICATION:
    /// - X-Api-Key header for simple integration
    /// - Authorization: Bearer <token> for JWT-based auth
    /// </summary>
    public static class StationApiEndpoints
    {
        public static void MapStationApi(this WebApplication app)
        {
            var api = app.MapGroup("/api/v1/stations")
                .WithTags("Stations");

            // Public endpoints (read-only)
            api.MapGet("/", GetAllStations)
                .WithName("GetAllStations")
                .WithSummary("Get list of connected stations");

            api.MapGet("/{stationId}/now-playing", GetNowPlaying)
                .WithName("GetNowPlaying")
                .WithSummary("Get current now playing information for a station");

            api.MapGet("/{stationId}/queue", GetQueueState)
                .WithName("GetQueueState")
                .WithSummary("Get current queue state for a station");

            // DJ endpoints (requires search/queue permission)
            api.MapPost("/{stationId}/library/search", SearchLibrary)
                .WithName("SearchLibrary")
                .WithSummary("Search the station's music library");

            api.MapPost("/{stationId}/queue/add", AddToQueue)
                .WithName("AddToQueue")
                .WithSummary("Add a track to the station's queue");

            api.MapPost("/{stationId}/requests", SubmitSongRequest)
                .WithName("SubmitSongRequest")
                .WithSummary("Submit a song request");

            // Admin endpoints (requires admin permission)
            api.MapPost("/{stationId}/queue/skip", SkipTrack)
                .WithName("SkipTrack")
                .WithSummary("Skip the currently playing track");

            api.MapDelete("/{stationId}/queue/{position}", RemoveFromQueue)
                .WithName("RemoveFromQueue")
                .WithSummary("Remove a track from the queue by position");

            api.MapDelete("/{stationId}/queue", ClearQueue)
                .WithName("ClearQueue")
                .WithSummary("Clear the entire queue");
        }

        // =====================================================================
        // PUBLIC ENDPOINTS (read permission)
        // =====================================================================

        private static IResult GetAllStations(
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request)
        {
            // This endpoint doesn't require auth but respects it if provided
            var connections = connectionManager.GetAllConnections()
                .Select(c => new
                {
                    stationId = c.StationId,
                    stationName = c.StationName,
                    isPlaying = c.CachedNowPlaying?.IsPlaying ?? false,
                    connectedAt = c.ConnectedAt
                })
                .ToList();

            return Results.Ok(new
            {
                count = connections.Count,
                stations = connections
            });
        }

        private static IResult GetNowPlaying(
            string stationId,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request)
        {
            // Public endpoint - no auth required
            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            var nowPlaying = connection.CachedNowPlaying ?? NowPlayingPayload.Empty();
            return Results.Ok(nowPlaying);
        }

        private static IResult GetQueueState(
            string stationId,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request)
        {
            // Public endpoint - no auth required
            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            var queueState = connection.CachedQueueState ?? new QueueStatePayload();
            return Results.Ok(queueState);
        }

        // =====================================================================
        // DJ ENDPOINTS (search/queue permission)
        // =====================================================================

        private static async Task<IResult> SearchLibrary(
            string stationId,
            [FromBody] LibrarySearchPayload searchRequest,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            // Requires search permission
            var identity = authService.AuthenticateRequest(request);
            if (identity == null || !identity.HasPermission(Permissions.Search))
            {
                return Results.Unauthorized();
            }

            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(searchRequest.Query) && 
                string.IsNullOrWhiteSpace(searchRequest.Artist) &&
                string.IsNullOrWhiteSpace(searchRequest.Album))
            {
                return Results.BadRequest(new { error = ErrorCodes.InvalidPayload, message = "Search query required" });
            }

            // Cap the limit
            searchRequest.Limit = Math.Clamp(searchRequest.Limit, 1, 200);

            // Send search request to desktop app and wait for response
            var requestMessage = MessageEnvelope.Create(
                MessageTypes.LibrarySearch,
                stationId,
                searchRequest,
                Guid.NewGuid().ToString("N"));

            var response = await connectionManager.SendAndWaitForResponseAsync(
                stationId,
                requestMessage,
                MessageTypes.LibrarySearchResult,
                cancellationToken);

            if (response == null)
            {
                return Results.StatusCode(504); // Gateway Timeout
            }

            // Check for error response
            if (response.Type == MessageTypes.Error)
            {
                var error = response.GetPayload<ErrorPayload>();
                return Results.BadRequest(new { error = error?.Code, message = error?.Message });
            }

            var result = response.GetPayload<LibrarySearchResultPayload>();
            return Results.Ok(result);
        }

        private static async Task<IResult> AddToQueue(
            string stationId,
            [FromBody] QueueAddPayload addRequest,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            // Requires queue permission
            var identity = authService.AuthenticateRequest(request);
            if (identity == null || !identity.HasPermission(Permissions.Queue))
            {
                return Results.Unauthorized();
            }

            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            // Validate
            if (string.IsNullOrWhiteSpace(addRequest.TrackId))
            {
                return Results.BadRequest(new { error = ErrorCodes.MissingField, message = "Track ID required" });
            }

            // Send add request
            var requestMessage = MessageEnvelope.Create(
                MessageTypes.QueueAdd,
                stationId,
                addRequest,
                Guid.NewGuid().ToString("N"));

            var response = await connectionManager.SendAndWaitForResponseAsync(
                stationId,
                requestMessage,
                MessageTypes.QueueAddResult,
                cancellationToken);

            if (response == null)
            {
                return Results.StatusCode(504);
            }

            if (response.Type == MessageTypes.Error)
            {
                var error = response.GetPayload<ErrorPayload>();
                return Results.BadRequest(new { error = error?.Code, message = error?.Message });
            }

            var result = response.GetPayload<QueueAddResultPayload>();
            if (result?.Success == true)
            {
                return Results.Ok(result);
            }
            else
            {
                return Results.BadRequest(result);
            }
        }

        private static async Task<IResult> SubmitSongRequest(
            string stationId,
            [FromBody] SongRequestPayload songRequest,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request,
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            // Song requests may be public or require auth depending on station config
            // For now, we'll make it public but rate-limited
            
            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            // Validate
            if (string.IsNullOrWhiteSpace(songRequest.TrackId))
            {
                return Results.BadRequest(new { error = ErrorCodes.MissingField, message = "Track ID required" });
            }

            if (string.IsNullOrWhiteSpace(songRequest.RequesterName))
            {
                return Results.BadRequest(new { error = ErrorCodes.MissingField, message = "Requester name required" });
            }

            // Set IP address from request (for rate limiting on the desktop side)
            songRequest.IpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            songRequest.RequestedAt = DateTimeOffset.UtcNow;

            // Send request
            var requestMessage = MessageEnvelope.Create(
                MessageTypes.SongRequest,
                stationId,
                songRequest,
                Guid.NewGuid().ToString("N"));

            var response = await connectionManager.SendAndWaitForResponseAsync(
                stationId,
                requestMessage,
                MessageTypes.SongRequestResult,
                cancellationToken);

            if (response == null)
            {
                return Results.StatusCode(504);
            }

            if (response.Type == MessageTypes.Error)
            {
                var error = response.GetPayload<ErrorPayload>();
                return Results.BadRequest(new { error = error?.Code, message = error?.Message });
            }

            var result = response.GetPayload<SongRequestResultPayload>();
            if (result?.Accepted == true)
            {
                return Results.Ok(result);
            }
            else
            {
                return Results.BadRequest(result);
            }
        }

        // =====================================================================
        // ADMIN ENDPOINTS (admin permission)
        // =====================================================================

        private static async Task<IResult> SkipTrack(
            string stationId,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            var identity = authService.AuthenticateRequest(request);
            if (identity == null || !identity.HasPermission(Permissions.Admin))
            {
                return Results.Unauthorized();
            }

            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            var skipMessage = MessageEnvelope.Create(MessageTypes.QueueSkip, stationId, new { });
            await connectionManager.SendMessageAsync(connection, skipMessage, cancellationToken);

            return Results.Ok(new { success = true, message = "Skip command sent" });
        }

        private static async Task<IResult> RemoveFromQueue(
            string stationId,
            int position,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            var identity = authService.AuthenticateRequest(request);
            if (identity == null || !identity.HasPermission(Permissions.Admin))
            {
                return Results.Unauthorized();
            }

            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            var removeMessage = MessageEnvelope.Create(
                MessageTypes.QueueRemove,
                stationId,
                new QueueRemovePayload { Position = position });

            await connectionManager.SendMessageAsync(connection, removeMessage, cancellationToken);

            return Results.Ok(new { success = true, message = $"Remove command sent for position {position}" });
        }

        private static async Task<IResult> ClearQueue(
            string stationId,
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            var identity = authService.AuthenticateRequest(request);
            if (identity == null || !identity.HasPermission(Permissions.Admin))
            {
                return Results.Unauthorized();
            }

            var connection = connectionManager.GetConnection(stationId);
            if (connection == null)
            {
                return Results.NotFound(new { error = ErrorCodes.StationOffline, message = "Station is not connected" });
            }

            var clearMessage = MessageEnvelope.Create(MessageTypes.QueueClear, stationId, new { });
            await connectionManager.SendMessageAsync(connection, clearMessage, cancellationToken);

            return Results.Ok(new { success = true, message = "Clear queue command sent" });
        }
    }
}
