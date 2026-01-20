using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenBroadcaster.RelayService.Configuration;

namespace OpenBroadcaster.RelayService.Services
{
    /// <summary>
    /// Permission levels for API access.
    /// </summary>
    public static class Permissions
    {
        /// <summary>Read-only access (now playing, queue state).</summary>
        public const string Read = "read";

        /// <summary>Library search access.</summary>
        public const string Search = "search";

        /// <summary>Queue manipulation (add, remove, skip).</summary>
        public const string Queue = "queue";

        /// <summary>Full administrative access.</summary>
        public const string Admin = "admin";

        /// <summary>All permissions.</summary>
        public const string All = "all";
    }

    /// <summary>
    /// Represents an authenticated API client.
    /// </summary>
    /// <summary>
    /// Represents an authenticated API client.
    /// </summary>
    public sealed class ApiClientIdentity
    {
        public string ClientId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();

        public bool HasPermission(string permission)
        {
            // Check for 'all' or 'admin' permission which grants everything
            return Permissions.Contains("all") ||
                   Permissions.Contains("admin") ||
                   Permissions.Contains(permission);
        }
    }

    /// <summary>
    /// Service for API authentication and authorization.
    /// 
    /// Supports two authentication modes:
    /// 1. API Key - Simple header-based auth for WordPress (X-Api-Key header)
    /// 2. JWT - Token-based auth for more complex integrations
    /// </summary>
    public sealed class AuthenticationService
    {
        private readonly RelayConfiguration _config;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            IOptions<RelayConfiguration> config,
            ILogger<AuthenticationService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Validates an API key and returns the client identity.
        /// </summary>
        public ApiClientIdentity? ValidateApiKey(string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            if (_config.ApiKeys.TryGetValue(apiKey, out var keyConfig))
            {
                _logger.LogDebug("API key validated: {Name}", keyConfig.Name);
                return new ApiClientIdentity
                {
                    ClientId = apiKey[..Math.Min(8, apiKey.Length)] + "...",
                    Name = keyConfig.Name,
                    Permissions = keyConfig.Permissions
                };
            }

            _logger.LogWarning("Invalid API key attempted");
            return null;
        }

        /// <summary>
        /// Generates a JWT token for an API client.
        /// </summary>
        public string GenerateJwtToken(ApiClientIdentity client)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Jwt.SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, client.ClientId),
                new(ClaimTypes.Name, client.Name),
            };

            // Add permission claims
            foreach (var permission in client.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var token = new JwtSecurityToken(
                issuer: _config.Jwt.Issuer,
                audience: _config.Jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_config.Jwt.TokenExpirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validates a JWT token and returns the client identity.
        /// </summary>
        public ApiClientIdentity? ValidateJwtToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            // Remove "Bearer " prefix if present
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = token[7..];
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_config.Jwt.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _config.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _config.Jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                var clientId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                var name = principal.FindFirstValue(ClaimTypes.Name) ?? "";
                var permissions = principal.FindAll("permission").Select(c => c.Value).ToList();

                return new ApiClientIdentity
                {
                    ClientId = clientId,
                    Name = name,
                    Permissions = permissions
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JWT validation failed");
                return null;
            }
        }

        /// <summary>
        /// Extracts and validates credentials from an HTTP request.
        /// Supports both API key and JWT authentication.
        /// </summary>
        public ApiClientIdentity? AuthenticateRequest(HttpRequest request)
        {
            // Try API key first (X-Api-Key header)
            if (request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
            {
                var apiKey = apiKeyValues.FirstOrDefault();
                var identity = ValidateApiKey(apiKey);
                if (identity != null)
                {
                    return identity;
                }
            }

            // Try Authorization header (Bearer token)
            if (request.Headers.TryGetValue("Authorization", out var authValues))
            {
                var authHeader = authValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    return ValidateJwtToken(authHeader);
                }
            }

            return null;
        }
    }
}
