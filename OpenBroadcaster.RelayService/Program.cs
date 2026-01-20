using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenBroadcaster.RelayService.Configuration;
using OpenBroadcaster.RelayService.Endpoints;
using OpenBroadcaster.RelayService.Services;

/*
 * ============================================================================
 * OpenBroadcaster Relay Service
 * ============================================================================
 * 
 * This is the central relay service for OpenBroadcaster remote control.
 * 
 * ARCHITECTURE:
 * - Desktop apps connect via outbound WebSocket (NAT-safe)
 * - WordPress/external clients connect via REST API
 * - The relay routes messages between the two
 * 
 * ENDPOINTS:
 * - /ws                         - WebSocket endpoint for desktop apps
 * - /api/v1/stations/...        - REST API for external clients
 * - /api/v1/auth/...            - Authentication endpoints
 * - /health                     - Health check
 * 
 * AUTHENTICATION:
 * - Desktop apps: Pre-shared station token
 * - REST clients: API key (X-Api-Key header) or JWT (Authorization: Bearer)
 * 
 * CONFIGURATION:
 * - See appsettings.json for all configuration options
 * - Use environment variables for secrets in production
 * 
 * ============================================================================
 */

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Configuration
// ============================================================================

builder.Services.Configure<RelayConfiguration>(
    builder.Configuration.GetSection(RelayConfiguration.SectionName));

var relayConfig = builder.Configuration
    .GetSection(RelayConfiguration.SectionName)
    .Get<RelayConfiguration>() ?? new RelayConfiguration();

// ============================================================================
// Services
// ============================================================================

// Core services
builder.Services.AddSingleton<StationConnectionManager>();
builder.Services.AddSingleton<AuthenticationService>();
builder.Services.AddTransient<WebSocketHandler>();

// Add CORS for WordPress integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()    // Configure appropriately for production
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add JWT authentication (optional - API keys work without this)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(relayConfig.Jwt.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = relayConfig.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = relayConfig.Jwt.Audience,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

// Add OpenAPI/Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OpenBroadcaster Relay API",
        Version = "v1",
        Description = "REST API for remote control of OpenBroadcaster stations"
    });

    // Add API key authentication to Swagger
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "API key for authentication"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================================
// Application
// ============================================================================

var app = builder.Build();

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Enable WebSockets
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// ============================================================================
// WebSocket Endpoint
// ============================================================================

app.Map("/ws", async (HttpContext context, WebSocketHandler handler) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await handler.HandleConnectionAsync(webSocket, context.RequestAborted);
});

// ============================================================================
// REST API Endpoints
// ============================================================================

app.MapSystemApi();
app.MapAuthApi();
app.MapStationApi();

// ============================================================================
// Startup
// ============================================================================

app.Logger.LogInformation("OpenBroadcaster Relay Service starting...");
app.Logger.LogInformation("WebSocket endpoint: /ws");
app.Logger.LogInformation("REST API base: /api/v1");

if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Swagger UI: /swagger");
}

app.Run();
