using Microsoft.AspNetCore.Mvc;
using OpenBroadcaster.RelayService.Services;

namespace OpenBroadcaster.RelayService.Endpoints
{
    /// <summary>
    /// Authentication endpoints for obtaining API tokens.
    /// </summary>
    public static class AuthEndpoints
    {
        public static void MapAuthApi(this WebApplication app)
        {
            var api = app.MapGroup("/api/v1/auth")
                .WithTags("Authentication");

            api.MapPost("/token", GetToken)
                .WithName("GetToken")
                .WithSummary("Exchange API key for a JWT token")
                .AllowAnonymous();

            api.MapGet("/validate", ValidateToken)
                .WithName("ValidateToken")
                .WithSummary("Validate current authentication");
        }

        /// <summary>
        /// Token request payload.
        /// </summary>
        public class TokenRequest
        {
            public string? ApiKey { get; set; }
        }

        /// <summary>
        /// Token response payload.
        /// </summary>
        public class TokenResponse
        {
            public string Token { get; set; } = string.Empty;
            public string TokenType { get; set; } = "Bearer";
            public int ExpiresIn { get; set; }
        }

        private static IResult GetToken(
            [FromBody] TokenRequest request,
            AuthenticationService authService)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return Results.BadRequest(new { error = "API key required" });
            }

            var identity = authService.ValidateApiKey(request.ApiKey);
            if (identity == null)
            {
                return Results.Unauthorized();
            }

            var token = authService.GenerateJwtToken(identity);

            return Results.Ok(new TokenResponse
            {
                Token = token,
                ExpiresIn = 3600 // Matches the config default
            });
        }

        private static IResult ValidateToken(
            AuthenticationService authService,
            HttpRequest request)
        {
            var identity = authService.AuthenticateRequest(request);
            if (identity == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                valid = true,
                clientId = identity.ClientId,
                name = identity.Name,
                permissions = identity.Permissions
            });
        }
    }
}
