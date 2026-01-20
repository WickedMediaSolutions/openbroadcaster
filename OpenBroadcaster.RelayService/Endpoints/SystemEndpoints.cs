using OpenBroadcaster.RelayService.Services;

namespace OpenBroadcaster.RelayService.Endpoints
{
    /// <summary>
    /// Health check and system status endpoints.
    /// </summary>
    public static class SystemEndpoints
    {
        public static void MapSystemApi(this WebApplication app)
        {
            app.MapGet("/health", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow
            }))
            .WithName("HealthCheck")
            .WithTags("System")
            .AllowAnonymous();

            app.MapGet("/api/v1/status", GetStatus)
                .WithName("GetStatus")
                .WithTags("System")
                .WithSummary("Get relay service status and statistics");
        }

        private static IResult GetStatus(
            StationConnectionManager connectionManager,
            AuthenticationService authService,
            HttpRequest request)
        {
            // Auth required for detailed status
            var identity = authService.AuthenticateRequest(request);
            var stats = connectionManager.GetStats();

            if (identity != null && identity.HasPermission(Permissions.Admin))
            {
                // Full status for admins
                var connections = connectionManager.GetAllConnections()
                    .Select(c => new
                    {
                        stationId = c.StationId,
                        stationName = c.StationName,
                        connectedAt = c.ConnectedAt,
                        lastHeartbeat = c.LastHeartbeat,
                        clientVersion = c.ClientVersion,
                        isPlaying = c.CachedNowPlaying?.IsPlaying ?? false,
                        queueCount = c.CachedQueueState?.TotalCount ?? 0
                    });

                return Results.Ok(new
                {
                    status = "running",
                    timestamp = DateTimeOffset.UtcNow,
                    totalConnections = stats.Total,
                    connectedStations = stats.Connected,
                    authenticatedStations = stats.Authenticated,
                    connections
                });
            }
            else
            {
                // Basic status for public
                return Results.Ok(new
                {
                    status = "running",
                    timestamp = DateTimeOffset.UtcNow,
                    connectedStations = stats.Connected
                });
            }
        }
    }
}
