using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;
using TagLib;

namespace OpenBroadcaster.Core.Overlay
{
    public sealed class OverlayDataServer : IDisposable
    {
        private readonly Func<OverlayStateSnapshot> _snapshotAccessor;
        private readonly Func<string?> _currentTrackFileAccessor;
        private readonly ILogger<OverlayDataServer> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly List<WebSocket> _sockets = new();
        private readonly object _socketLock = new();
        private OverlaySettings _settings = new();
        private HttpListener? _listener;
        private CancellationTokenSource? _listenerCts;
        private Task? _listenerTask;
        private int _currentPort;
        private string _staticRoot = string.Empty;
        private string _fallbackArtworkPath = string.Empty;
        private bool _disposed;

        public OverlayDataServer(Func<OverlayStateSnapshot> snapshotAccessor, Func<string?>? currentTrackFileAccessor = null, ILogger<OverlayDataServer>? logger = null)
        {
            _snapshotAccessor = snapshotAccessor ?? throw new ArgumentNullException(nameof(snapshotAccessor));
            _currentTrackFileAccessor = currentTrackFileAccessor ?? (() => null);
            _logger = logger ?? AppLogger.CreateLogger<OverlayDataServer>();
        }

        public void ApplySettings(OverlaySettings? settings)
        {
            _settings = settings?.Clone() ?? new OverlaySettings();
            _fallbackArtworkPath = _settings.ArtworkFallbackFilePath ?? string.Empty;

            if (!_settings.Enabled)
            {
                Stop();
                return;
            }

            if (_listener != null && _currentPort == _settings.Port)
            {
                return;
            }

            Restart();
        }

        public void Publish(OverlayStateSnapshot snapshot)
        {
            if (!_settings.Enabled || snapshot == null)
            {
                return;
            }

            List<WebSocket> recipients;
            lock (_socketLock)
            {
                recipients = _sockets.Where(socket => socket.State == WebSocketState.Open).ToList();
            }

            if (recipients.Count == 0)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(snapshot, _jsonOptions);
            var buffer = Encoding.UTF8.GetBytes(payload);

            foreach (var socket in recipients)
            {
                _ = SendAsync(socket, buffer);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
            _listener?.Close();
        }

        private void Restart()
        {
            Stop();

            _staticRoot = Path.Combine(AppContext.BaseDirectory, "Overlay");
            if (!Directory.Exists(_staticRoot))
            {
                Directory.CreateDirectory(_staticRoot);
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_settings.Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_settings.Port}/");
            _listener.Start();
            _listenerCts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenAsync(_listenerCts.Token));
            _currentPort = _settings.Port;
            _logger.LogInformation("Overlay server listening on port {Port}", _settings.Port);
        }

        private void Stop()
        {
            try
            {
                _listenerCts?.Cancel();
            }
            catch
            {
                // Ignore cancellation errors.
            }

            lock (_socketLock)
            {
                foreach (var socket in _sockets)
                {
                    try
                    {
                        socket.Abort();
                    }
                    catch
                    {
                        // Ignore socket disposal issues.
                    }
                }

                _sockets.Clear();
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
                // Listener might already be stopped.
            }

            _listener = null;
            _listenerTask = null;
            _listenerCts?.Dispose();
            _listenerCts = null;
            _currentPort = 0;
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener!.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Overlay listener accept loop failed");
                    break;
                }

                if (context == null)
                {
                    continue;
                }

                _ = Task.Run(() => HandleContextAsync(context), token);
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            try
            {
                // Add CORS headers for all responses
                AddCorsHeaders(context.Response);
                
                var path = context.Request.Url?.AbsolutePath ?? "/";
                
                // Handle CORS preflight requests
                if (context.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Close();
                    return;
                }

                if (IsWebSocketPath(context.Request))
                {
                    await HandleWebSocketAsync(context).ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/api/state", StringComparison.OrdinalIgnoreCase))
                {
                    await ServeStateAsync(context).ConfigureAwait(false);
                    return;
                }

                await ServeStaticAsync(context, path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Overlay request handling failed");
                TryWriteStatus(context, HttpStatusCode.InternalServerError, "Internal Server Error");
            }
        }
        
        /// <summary>
        /// Adds CORS headers to allow cross-origin requests (e.g., from WordPress plugins).
        /// </summary>
        private void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept");
            response.Headers.Add("Access-Control-Max-Age", "86400"); // Cache preflight for 24 hours
        }

        private bool IsWebSocketPath(HttpListenerRequest request)
        {
            if (request == null)
            {
                return false;
            }

            return request.IsWebSocketRequest &&
                   string.Equals(request.Url?.AbsolutePath, "/ws", StringComparison.OrdinalIgnoreCase);
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                var socket = wsContext.WebSocket;
                lock (_socketLock)
                {
                    _sockets.Add(socket);
                }

                _logger.LogInformation("Overlay client connected (total {Count})", _sockets.Count);
                await SendInitialStateAsync(socket).ConfigureAwait(false);
                await ReceiveUntilClosedAsync(socket).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Overlay websocket negotiation failed");
                TryWriteStatus(context, HttpStatusCode.BadRequest, "WebSocket negotiation failed");
            }
        }

        private async Task SendInitialStateAsync(WebSocket socket)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            var snapshot = _snapshotAccessor();
            var payload = JsonSerializer.Serialize(snapshot, _jsonOptions);
            var buffer = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task ReceiveUntilClosedAsync(WebSocket socket)
        {
            try
            {
                var buffer = new byte[256];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // Non-fatal: socket errors are expected when clients disconnect abruptly.
            }
            finally
            {
                lock (_socketLock)
                {
                    _sockets.Remove(socket);
                }

                try
                {
                    if (socket.State != WebSocketState.Closed)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                    socket.Abort();
                }

                _logger.LogInformation("Overlay client disconnected (remaining {Count})", _sockets.Count);
            }
        }

        private async Task SendAsync(WebSocket socket, byte[] buffer)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                lock (_socketLock)
                {
                    _sockets.Remove(socket);
                }

                try
                {
                    socket.Abort();
                }
                catch
                {
                    // Ignore.
                }
            }
        }

        private async Task ServeStateAsync(HttpListenerContext context)
        {
            var snapshot = _snapshotAccessor();
            var payload = JsonSerializer.Serialize(snapshot, _jsonOptions);
            var buffer = Encoding.UTF8.GetBytes(payload);
            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }

        private async Task ServeStaticAsync(HttpListenerContext context, string path)
        {
            if (IsTrackArtworkRequest(path))
            {
                await ServeTrackArtworkAsync(context).ConfigureAwait(false);
                return;
            }

            if (IsCustomArtworkRequest(path))
            {
                await ServeCustomArtworkAsync(context).ConfigureAwait(false);
                return;
            }

            var relative = NormalizePath(path);
            var fullPath = Path.Combine(_staticRoot, relative);
            if (!IsUnderRoot(fullPath))
            {
                TryWriteStatus(context, HttpStatusCode.Forbidden, "Forbidden");
                return;
            }

            if (Directory.Exists(fullPath))
            {
                fullPath = Path.Combine(fullPath, "index.html");
            }

            if (!System.IO.File.Exists(fullPath))
            {
                TryWriteStatus(context, HttpStatusCode.NotFound, "Not Found");
                return;
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
            context.Response.ContentType = ResolveContentType(fullPath);
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }

        private bool IsCustomArtworkRequest(string path)
        {
            return string.Equals(path, OverlayPaths.CustomArtwork, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTrackArtworkRequest(string path)
        {
            return string.Equals(path, OverlayPaths.TrackArtwork, StringComparison.OrdinalIgnoreCase);
        }

        private async Task ServeTrackArtworkAsync(HttpListenerContext context)
        {
            var filePath = _currentTrackFileAccessor();
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                await ServeStaticAsync(context, OverlayPaths.DefaultArtwork).ConfigureAwait(false);
                return;
            }

            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var pictures = tagFile.Tag?.Pictures;

                if (pictures == null || pictures.Length == 0)
                {
                    await ServeStaticAsync(context, OverlayPaths.DefaultArtwork).ConfigureAwait(false);
                    return;
                }

                // Prefer front cover, fall back to first available
                IPicture? picture = null;
                foreach (var pic in pictures)
                {
                    if (pic.Type == PictureType.FrontCover)
                    {
                        picture = pic;
                        break;
                    }
                    picture ??= pic;
                }

                if (picture?.Data?.Data == null || picture.Data.Data.Length == 0)
                {
                    await ServeStaticAsync(context, OverlayPaths.DefaultArtwork).ConfigureAwait(false);
                    return;
                }

                var bytes = picture.Data.Data;
                context.Response.ContentType = picture.MimeType ?? "image/jpeg";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract track artwork from {FilePath}", filePath);
                await ServeStaticAsync(context, OverlayPaths.DefaultArtwork).ConfigureAwait(false);
            }
        }

        private async Task ServeCustomArtworkAsync(HttpListenerContext context)
        {
            var candidate = _fallbackArtworkPath;
            if (string.IsNullOrWhiteSpace(candidate) || !System.IO.File.Exists(candidate))
            {
                await ServeStaticAsync(context, OverlayPaths.DefaultArtwork).ConfigureAwait(false);
                return;
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(candidate).ConfigureAwait(false);
            context.Response.ContentType = ResolveContentType(candidate);
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "/")
            {
                return "index.html";
            }

            return path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        }

        private bool IsUnderRoot(string candidate)
        {
            if (string.IsNullOrEmpty(_staticRoot))
            {
                return false;
            }

            var fullRoot = Path.GetFullPath(_staticRoot);
            var fullCandidate = Path.GetFullPath(candidate);
            return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".svg" => "image/svg+xml",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }

        private static void TryWriteStatus(HttpListenerContext context, HttpStatusCode code, string message)
        {
            if (context == null)
            {
                return;
            }

            try
            {
                context.Response.StatusCode = (int)code;
                using var writer = new StreamWriter(context.Response.OutputStream);
                writer.Write(message);
                context.Response.Close();
            }
            catch
            {
                // Ignore failures when client disconnects early.
            }
        }
    }
}
