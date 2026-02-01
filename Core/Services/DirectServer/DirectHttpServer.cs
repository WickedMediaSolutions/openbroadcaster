using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using OpenBroadcaster.Core.Models;
using Microsoft.Extensions.Logging;

namespace OpenBroadcaster.Core.Services.DirectServer;

/// <summary>
/// Snapshot of current playback state for the Direct Server API.
/// </summary>
public class DirectServerSnapshot
{
    public DirectServerDtos.NowPlayingResponse? NowPlaying { get; set; }
    public List<DirectServerDtos.QueueItem> Queue { get; set; } = new();
}

/// <summary>
/// Library item for search results.
/// </summary>
public class DirectServerLibraryItem
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? FilePath { get; set; }
}

/// <summary>
/// Song request from the API.
/// </summary>
public class SongRequest
{
    public Guid TrackId { get; set; }
    public string? RequesterName { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Embedded HTTP server that allows direct connections to the desktop app.
/// Provides REST API endpoints for now playing, queue, library search, and song requests.
/// Alternative to the relay service for same-network or port-forwarded setups.
/// </summary>
public class DirectHttpServer : IDisposable
{
    private readonly DirectServerSettings _settings;
    private readonly ILogger<DirectHttpServer>? _logger;
    
    // Callbacks for getting data
    private readonly Func<DirectServerSnapshot>? _getSnapshot;
    private readonly Func<string, int, int, IEnumerable<DirectServerLibraryItem>>? _searchLibrary;
    private readonly Action<SongRequest>? _onSongRequest;
    private readonly Func<string>? _getStationName;
    
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isRunning;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    /// <summary>
    /// Event raised when the server starts.
    /// </summary>
    public event EventHandler? ServerStarted;

    /// <summary>
    /// Event raised when the server stops.
    /// </summary>
    public event EventHandler? ServerStopped;

    /// <summary>
    /// Event raised when a request is received (for logging/UI updates).
    /// </summary>
    public event EventHandler<string>? RequestReceived;

    /// <summary>
    /// Event raised when a song request is submitted.
    /// </summary>
    public event EventHandler<SongRequest>? SongRequestReceived;

    /// <summary>
    /// Whether the server is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// The URL the server is listening on.
    /// </summary>
    public string? ListeningUrl => GetLocalUrl();
    
    /// <summary>
    /// Gets the local URL for the server.
    /// </summary>
    public string GetLocalUrl()
    {
        return $"http://localhost:{_settings.Port}/";
    }

    /// <summary>
    /// Gets the base URL for binding (uses + for remote connections).
    /// </summary>
    private string GetBaseUrl()
    {
        var host = _settings.AllowRemoteConnections ? "+" : "localhost";
        return $"http://{host}:{_settings.Port}/";
    }

    public DirectHttpServer(
        DirectServerSettings settings,
        Func<DirectServerSnapshot>? getSnapshot = null,
        Func<string, int, int, IEnumerable<DirectServerLibraryItem>>? searchLibrary = null,
        Action<SongRequest>? onSongRequest = null,
        Func<string>? getStationName = null,
        ILogger<DirectHttpServer>? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _getSnapshot = getSnapshot;
        _searchLibrary = searchLibrary;
        _onSongRequest = onSongRequest;
        _getStationName = getStationName;
        _logger = logger;
        // create a single listener instance for the lifetime of this service to avoid recreated HttpListener disposal issues
        try
        {
            _listener = new HttpListener();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create HttpListener in constructor");
            _listener = null;
        }
    }

    /// <summary>
    /// Starts the HTTP server.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _logger?.LogWarning("Direct server is already running");
                return;
            }

            try
            {
                // Use the single listener instance created in the constructor. Clear and re-add prefixes each start.
                if (_listener == null)
                {
                    _listener = new HttpListener();
                }
                var baseUrl = GetBaseUrl();
                try
                {
                    _listener.Prefixes.Clear();
                    _listener.Prefixes.Add(baseUrl);
                    _listener.Start();
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 5 && baseUrl.Contains("+"))
                {
                    // Access denied when attempting to bind to '+' (all interfaces). Try localhost-only fallback
                    _logger?.LogWarning(ex, "Access denied binding to {BaseUrl}; falling back to localhost-only binding", baseUrl);
                    var localUrl = $"http://localhost:{_settings.Port}/";
                    _listener.Prefixes.Clear();
                    _listener.Prefixes.Add(localUrl);
                    _listener.Start();
                }

                // If the listen loop is already running, just flip the running flag so it begins handling requests again.
                if (_listenerTask != null && !_listenerTask.IsCompleted)
                {
                    _isRunning = true;
                    _logger?.LogDebug("Direct HTTP server resumed on {Url}", GetLocalUrl());
                    ServerStarted?.Invoke(this, EventArgs.Empty);
                    return;
                }

                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenLoop(_listener, _cts.Token));
                _isRunning = true;

                _logger?.LogInformation("Direct HTTP server started on {Url}", GetLocalUrl());
                ServerStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
            {
                _logger?.LogError("Access denied starting HTTP server. Run as admin or use: netsh http add urlacl url={Url} user=Everyone", GetBaseUrl());
                throw new InvalidOperationException(
                    $"Cannot start server on port {_settings.Port}. " +
                    "Either run as administrator, or run this command in an admin PowerShell:\n" +
                    $"netsh http add urlacl url={GetBaseUrl()} user=Everyone", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start direct HTTP server");
                // Ensure partially-initialized listener is cleaned up
                try { _listener?.Close(); } catch { }
                _listener = null;
                _cts = null;
                _listenerTask = null;
                throw;
            }
        }
    }

    /// <summary>
    /// Stops the HTTP server.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            try
            {
                // Rather than disposing/closing the underlying HttpListener (which can cause ObjectDisposed errors
                // when creating new instances), we simply mark the server as not running. The listen loop stays
                // active and will return 503 for incoming requests while stopped. This allows fast Stop/Start
                // cycles on the same DirectHttpServer instance without recreating the HttpListener.
                _isRunning = false;
                _logger?.LogDebug("Direct HTTP server paused");
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping HTTP server");
            }
            finally
            {
                _isRunning = false;
                _cts = null;
                _listenerTask = null;
                
                _logger?.LogInformation("Direct HTTP server stopped");
                ServerStopped?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private async Task ListenLoop(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (listener == null || !listener.IsListening)
                    break;

                var context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequest(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in HTTP listener loop");
                // Small delay to avoid tight loop on repeated errors
                try { await Task.Delay(1000, cancellationToken).ConfigureAwait(false); } catch { break; }
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // If server is paused, return 503 Service Unavailable
            if (!_isRunning)
            {
                await SendError(response, 503, "Server is stopped");
                return;
            }
            // Log request
            var endpoint = $"{request.HttpMethod} {request.Url?.PathAndQuery}";
            RequestReceived?.Invoke(this, endpoint);
            _logger?.LogDebug("Request: {Endpoint} from {RemoteEndpoint}", endpoint, request.RemoteEndPoint);

            // CORS headers
            if (_settings.EnableCors)
            {
                response.Headers.Add("Access-Control-Allow-Origin", _settings.CorsOrigins);
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-API-Key, Authorization");
            }

            // Handle preflight
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            // Check API key if configured
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                var providedKey = request.Headers["X-API-Key"] ?? request.QueryString["api_key"];
                if (providedKey != _settings.ApiKey)
                {
                    await SendError(response, 401, "Invalid or missing API key");
                    return;
                }
            }

            // Route request
            var path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";
            
            switch (path)
            {
                case "/":
                case "/api":
                case "/api/":
                    await HandleStatus(response);
                    break;

                case "/api/status":
                    await HandleStatus(response);
                    break;

                case "/api/now-playing":
                case "/api/nowplaying":
                    await HandleNowPlaying(response);
                    break;

                case "/api/queue":
                    await HandleQueue(response);
                    break;

                case "/api/library/search":
                    await HandleLibrarySearch(request, response);
                    break;

                case "/api/requests":
                    if (request.HttpMethod == "POST")
                    {
                        await HandleSongRequest(request, response);
                    }
                    else
                    {
                        await SendError(response, 405, "Method not allowed");
                    }
                    break;

                default:
                    await SendError(response, 404, "Endpoint not found");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling request");
            try
            {
                await SendError(response, 500, "Internal server error");
            }
            catch { /* Ignore errors sending error response */ }
        }
        finally
        {
            try { response.Close(); } catch { }
        }
    }

    private Task HandleStatus(HttpListenerResponse response)
    {
        var status = new DirectServerDtos.StatusResponse
        {
            Status = "online",
            Version = "1.3.0",
            StationName = _getStationName?.Invoke() ?? "OpenBroadcaster",
            RequestsEnabled = true,
            ServerTime = DateTime.UtcNow
        };
        return SendJson(response, status);
    }

    private Task HandleNowPlaying(HttpListenerResponse response)
    {
        var snapshot = _getSnapshot?.Invoke();
        var nowPlaying = snapshot?.NowPlaying ?? new DirectServerDtos.NowPlayingResponse();
        nowPlaying.Timestamp = DateTime.UtcNow;
        return SendJson(response, nowPlaying);
    }

    private Task HandleQueue(HttpListenerResponse response)
    {
        var snapshot = _getSnapshot?.Invoke();
        var queueResponse = new DirectServerDtos.QueueResponse
        {
            Items = snapshot?.Queue ?? new List<DirectServerDtos.QueueItem>(),
            TotalCount = snapshot?.Queue.Count ?? 0
        };
        return SendJson(response, queueResponse);
    }

    private Task HandleLibrarySearch(HttpListenerRequest request, HttpListenerResponse response)
    {
        var query = request.QueryString["q"] ?? request.QueryString["query"] ?? string.Empty;
        var pageStr = request.QueryString["page"] ?? "1";
        var perPageStr = request.QueryString["per_page"] ?? "20";
        
        if (!int.TryParse(pageStr, out var page) || page < 1) page = 1;
        if (!int.TryParse(perPageStr, out var perPage) || perPage < 1) perPage = 20;
        perPage = Math.Min(perPage, 100);

        var searchResponse = new DirectServerDtos.LibrarySearchResponse
        {
            Page = page,
            PerPage = perPage
        };

        if (_searchLibrary != null)
        {
            // Allow empty query to return a paged browse view of the library
            var items = _searchLibrary(query, page, perPage).ToList();
            searchResponse.Items = items.Select(i => new DirectServerDtos.LibraryItem
            {
                Id = i.Id.ToString(),
                Title = i.Title,
                Artist = i.Artist,
                Album = i.Album,
                Duration = (int)(i.Duration?.TotalSeconds ?? 0),
                FilePath = i.FilePath,
                Type = "track"
            }).ToList();
            searchResponse.TotalItems = items.Count; // In real impl, get total from search
            searchResponse.TotalPages = (searchResponse.TotalItems + perPage - 1) / perPage;
        }

        return SendJson(response, searchResponse);
    }

    private async Task HandleSongRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            
            var submission = JsonSerializer.Deserialize<DirectServerDtos.SongRequestSubmission>(body, JsonOptions);
            if (submission == null || string.IsNullOrEmpty(submission.TrackId))
            {
                await SendError(response, 400, "Invalid request body");
                return;
            }

            if (!Guid.TryParse(submission.TrackId, out var trackId))
            {
                await SendError(response, 400, "Invalid track ID");
                return;
            }

            var songRequest = new SongRequest
            {
                TrackId = trackId,
                RequesterName = submission.RequesterName ?? "Website",
                Message = submission.Message,
                Timestamp = DateTime.UtcNow
            };

            _onSongRequest?.Invoke(songRequest);
            SongRequestReceived?.Invoke(this, songRequest);

            var requestResponse = new DirectServerDtos.SongRequestResponse
            {
                Success = true,
                RequestId = Guid.NewGuid().ToString(),
                Message = "Request submitted successfully"
            };

            await SendJson(response, requestResponse);
        }
        catch (JsonException)
        {
            await SendError(response, 400, "Invalid JSON");
        }
    }

    private async Task SendJson<T>(HttpListenerResponse response, T data)
    {
        response.ContentType = "application/json";
        response.StatusCode = 200;
        
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);
        
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private async Task SendError(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var error = new DirectServerDtos.ErrorResponse
        {
            Success = false,
            Error = message,
            Code = statusCode
        };

        var json = JsonSerializer.Serialize(error, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    public void Dispose()
    {
        // Stop and then close the listener instance for good.
        Stop();
        try { _listener?.Close(); } catch { }
        _listener = null;
    }
}
