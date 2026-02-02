using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if NET8_0_WINDOWS
using NAudio.Lame;
#endif
using NAudio.Wave;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Streaming
{
    public sealed class EncoderManager : IDisposable
    {
        private readonly ILogger<EncoderManager> _logger;
        private readonly IEncoderAudioSourceFactory _audioSourceFactory;
        private readonly Dictionary<Guid, EncoderWorker> _workers = new();
        private readonly Dictionary<Guid, EncoderStatus> _statusCache = new();
        private readonly object _gate = new();
        private List<EncoderProfile> _profiles = new();
        private IEncoderAudioSource? _audioSource;
        private EncoderMetadata? _metadata;
        private CancellationTokenSource? _cts;
        private int _captureDeviceId = -1;
        private bool _isRunning;
        private bool _disposed;

        public EncoderManager(IEncoderAudioSourceFactory? audioSourceFactory = null, ILogger<EncoderManager>? logger = null)
        {
            _audioSourceFactory = audioSourceFactory ?? new EncoderAudioSourceFactory();
            _logger = logger ?? AppLogger.CreateLogger<EncoderManager>();
        }

        public event EventHandler<EncoderStatusChangedEventArgs>? StatusChanged;

        public bool IsRunning => _isRunning;

        public void UpdateConfiguration(EncoderSettings? settings, int captureDeviceId)
        {
            bool shouldRestart;
            var profiles = CloneProfiles(settings?.Profiles);
            _logger.LogInformation("Encoder configuration updated: {ProfileCount} profile(s), capture device {DeviceId}", profiles.Count, captureDeviceId);

            lock (_gate)
            {
                _captureDeviceId = captureDeviceId;
                _profiles = profiles;
                shouldRestart = _isRunning;
            }

            PublishConfigurationSnapshot();

            if (shouldRestart)
            {
                Restart();
            }
        }

        public void UpdateNowPlayingMetadata(Track? track)
        {
            EncoderMetadata? metadata = track == null
                ? null
                : new EncoderMetadata(track.Title ?? string.Empty, track.Artist ?? string.Empty, track.Album ?? string.Empty);

            lock (_gate)
            {
                _metadata = metadata;
                foreach (var worker in _workers.Values)
                {
                    worker.UpdateMetadata(metadata);
                }
            }
        }

        public IReadOnlyCollection<EncoderStatus> SnapshotStatuses()
        {
            lock (_gate)
            {
                return _statusCache.Values.ToList();
            }
        }

        public void Start()
        {
            List<EncoderProfile> enabledProfiles;

            lock (_gate)
            {
                if (_isRunning)
                {
                    _logger.LogDebug("EncoderManager already running");
                    return;
                }

                enabledProfiles = _profiles.Where(profile => profile.Enabled).ToList();
                if (enabledProfiles.Count == 0)
                {
                    throw new InvalidOperationException("No enabled encoder profiles are configured.");
                }

                foreach (var profile in enabledProfiles)
                {
                    _logger.LogInformation(
                        "Encoder profile '{Name}' targeting {Host}:{Port}{Mount} (Protocol={Protocol}, SSL={UseSsl}, Bitrate={Bitrate}kbps)",
                        profile.Name,
                        string.IsNullOrWhiteSpace(profile.Host) ? "localhost" : profile.Host,
                        profile.Port,
                        string.IsNullOrWhiteSpace(profile.Mount) ? string.Empty : profile.Mount,
                        profile.Protocol,
                        profile.UseSsl,
                        profile.BitrateKbps);
                }

                _cts = new CancellationTokenSource();
                try
                {
                    _audioSource = _audioSourceFactory.Create(_captureDeviceId);
                    _audioSource.FrameReady += OnAudioFrameReady;
                    _audioSource.Start();

                    foreach (var profile in enabledProfiles)
                    {
                        var worker = new EncoderWorker(profile, _audioSource.Format, this, _logger, _cts.Token);
                        _workers[profile.Id] = worker;
                        worker.Start();
                        worker.UpdateMetadata(_metadata);
                    }

                    _isRunning = true;
                }
                catch (Exception ex)
                {
                    PublishErrorStatuses($"Encoder start failed: {ex.Message}");
                    _logger.LogError(ex, "EncoderManager failed to start");

                    if (_audioSource != null)
                    {
                        _audioSource.FrameReady -= OnAudioFrameReady;
                        _audioSource.Stop();
                        _audioSource.Dispose();
                        _audioSource = null;
                    }

                    if (_cts != null)
                    {
                        _cts.Cancel();
                        _cts.Dispose();
                        _cts = null;
                    }

                    StopWorkers();
                    throw;
                }
            }

            _logger.LogInformation("EncoderManager started with {Count} profile(s)", enabledProfiles.Count);
        }

        public void Stop()
        {
            Dictionary<Guid, EncoderWorker> workersSnapshot;
            IEncoderAudioSource? source;
            CancellationTokenSource? cts;

            lock (_gate)
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;
                workersSnapshot = new Dictionary<Guid, EncoderWorker>(_workers);
                _workers.Clear();
                source = _audioSource;
                _audioSource = null;
                cts = _cts;
                _cts = null;
            }

            try
            {
                cts?.Cancel();
            }
            catch
            {
                // Ignore cancellation errors during shutdown.
            }

            foreach (var worker in workersSnapshot.Values)
            {
                worker.Dispose();
            }

            if (source != null)
            {
                source.FrameReady -= OnAudioFrameReady;
                source.Stop();
                source.Dispose();
            }

            cts?.Dispose();
            PublishStoppedStatuses();
            _logger.LogInformation("EncoderManager stopped");
        }

        public void Restart()
        {
            Stop();
            try
            {
                Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encoder restart failed");
                PublishErrorStatuses(ex.Message);
            }
        }

        private void PublishConfigurationSnapshot()
        {
            List<EncoderProfile> snapshot;
            lock (_gate)
            {
                snapshot = new List<EncoderProfile>(_profiles);
            }

            foreach (var profile in snapshot)
            {
                var status = profile.Enabled
                    ? new EncoderStatus(profile.Id, profile.Name, EncoderState.Stopped, "Ready", TryGetLastConnected(profile.Id))
                    : new EncoderStatus(profile.Id, profile.Name, EncoderState.Stopped, "Profile disabled", TryGetLastConnected(profile.Id));
                PublishStatus(status);
            }

            var validIds = snapshot.Select(profile => profile.Id).ToHashSet();
            lock (_gate)
            {
                var stale = _statusCache.Keys.Where(id => !validIds.Contains(id)).ToList();
                foreach (var id in stale)
                {
                    _statusCache.Remove(id);
                }
            }
        }

        private void PublishStoppedStatuses()
        {
            List<EncoderProfile> snapshot;
            lock (_gate)
            {
                snapshot = new List<EncoderProfile>(_profiles);
            }

            foreach (var profile in snapshot)
            {
                PublishStatus(new EncoderStatus(profile.Id, profile.Name, EncoderState.Stopped, "Stopped", TryGetLastConnected(profile.Id)));
            }
        }

        private void PublishErrorStatuses(string message)
        {
            List<EncoderProfile> snapshot;
            lock (_gate)
            {
                snapshot = new List<EncoderProfile>(_profiles);
            }

            foreach (var profile in snapshot.Where(profile => profile.Enabled))
            {
                PublishStatus(new EncoderStatus(profile.Id, profile.Name, EncoderState.Failed, message, TryGetLastConnected(profile.Id)));
            }
        }

        private DateTimeOffset? TryGetLastConnected(Guid profileId)
        {
            lock (_gate)
            {
                return _statusCache.TryGetValue(profileId, out var status) ? status.LastConnected : null;
            }
        }

        private static List<EncoderProfile> CloneProfiles(IEnumerable<EncoderProfile>? profiles)
        {
            if (profiles == null)
            {
                return new List<EncoderProfile>();
            }

            return profiles
                .Select(profile => new EncoderProfile
                {
                    Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
                    Name = profile.Name ?? string.Empty,
                    Host = profile.Host ?? string.Empty,
                    Port = profile.Port,
                    Mount = profile.Mount ?? string.Empty,
                    Username = profile.Username ?? string.Empty,
                    Password = profile.Password ?? string.Empty,
                    Enabled = profile.Enabled,
                    UseSsl = profile.UseSsl,
                    BitrateKbps = profile.BitrateKbps,
                    Format = profile.Format,
                    Protocol = profile.Protocol,
                    Description = profile.Description ?? string.Empty,
                    Genre = profile.Genre ?? string.Empty,
                    Public = profile.Public
                })
                .ToList();
        }

        internal void PublishStatus(EncoderStatus status)
        {
            lock (_gate)
            {
                _statusCache[status.ProfileId] = status;
            }

            StatusChanged?.Invoke(this, new EncoderStatusChangedEventArgs(status));
        }

        private void StopWorkers()
        {
            foreach (var worker in _workers.Values)
            {
                worker.Dispose();
            }

            _workers.Clear();
        }

        private void OnAudioFrameReady(object? sender, EncoderAudioFrameEventArgs e)
        {
            EncoderWorker[] workers;
            lock (_gate)
            {
                if (!_isRunning || _workers.Count == 0)
                {
                    e.Dispose();
                    return;
                }

                workers = _workers.Values.ToArray();
            }

            foreach (var worker in workers)
            {
                worker.EnqueueFrame(e.Buffer, e.BytesRecorded);
            }

            e.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        private sealed class EncoderWorker : IDisposable
        {
            private const int MaxBackoffSeconds = 30;
            private readonly EncoderProfile _profile;
            private readonly WaveFormat _waveFormat;
            private readonly EncoderManager _owner;
            private readonly ILogger _logger;
            private readonly CancellationToken _cancellationToken;
            private readonly Channel<EncoderBuffer> _channel;
            private Task? _processingTask;
            private bool _acceptFrames;
            private bool _disposed;
            private int _backoffSeconds = 2;
            private DateTimeOffset? _lastConnected;
            private EncoderMetadata? _pendingMetadata;
            private string? _lastMetadataPayload;

            public EncoderWorker(EncoderProfile profile, WaveFormat format, EncoderManager owner, ILogger logger, CancellationToken cancellationToken)
            {
                _profile = profile;
                _waveFormat = format;
                _owner = owner;
                _logger = logger;
                _cancellationToken = cancellationToken;
                _channel = Channel.CreateBounded<EncoderBuffer>(new BoundedChannelOptions(64)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });
            }

            public void Start()
            {
                _processingTask = Task.Run(ProcessAsync, CancellationToken.None);
            }

            public void EnqueueFrame(byte[] buffer, int length)
            {
                if (!_acceptFrames || length <= 0)
                {
                    return;
                }

                var rented = ArrayPool<byte>.Shared.Rent(length);
                Buffer.BlockCopy(buffer, 0, rented, 0, length);
                if (!_channel.Writer.TryWrite(new EncoderBuffer(rented, length)))
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            public void UpdateMetadata(EncoderMetadata? metadata)
            {
                _pendingMetadata = metadata;
                if (_acceptFrames)
                {
                    _ = Task.Run(() => TrySendMetadataAsync(metadata, _cancellationToken), CancellationToken.None);
                }
            }

            private async Task ProcessAsync()
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    TcpClient? client = null;
                    Stream? stream = null;
                    Mp3Encoder? encoder = null;

                    try
                    {
                        _logger.LogInformation(
                            "Encoder '{Name}' attempting connection to {Host}:{Port} (Protocol={Protocol}, SSL={UseSsl})",
                            _profile.Name,
                            string.IsNullOrWhiteSpace(_profile.Host) ? "localhost" : _profile.Host,
                            _profile.Port,
                            _profile.Protocol,
                            _profile.UseSsl);
                        _owner.PublishStatus(new EncoderStatus(_profile.Id, _profile.Name, EncoderState.Connecting, "Connecting...", _lastConnected));
                        (client, stream) = await CreateNetworkStreamAsync(_cancellationToken).ConfigureAwait(false);
                        encoder = new Mp3Encoder(_waveFormat, Math.Max(32, _profile.BitrateKbps), stream);
                        _acceptFrames = true;
                        _lastConnected = DateTimeOffset.UtcNow;
                        _logger.LogInformation("Encoder '{Name}' handshake completed; entering streaming state", _profile.Name);
                        _owner.PublishStatus(new EncoderStatus(_profile.Id, _profile.Name, EncoderState.Streaming, "Streaming", _lastConnected));
                        _backoffSeconds = 2;
                        await TrySendMetadataAsync(_pendingMetadata, _cancellationToken).ConfigureAwait(false);

                        while (!_cancellationToken.IsCancellationRequested)
                        {
                            var frame = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
                            try
                            {
                                encoder.Encode(frame.Buffer, frame.Length);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(frame.Buffer);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ChannelClosedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        var logPath = EncoderDiagnostics.RecordException(_profile, ex);
                        _logger.LogError(ex, "Encoder '{Name}' encountered an exception. Details saved to {LogPath}.", _profile.Name, logPath);
                        _acceptFrames = false;
                        DrainChannel();
                        _owner.PublishStatus(new EncoderStatus(_profile.Id, _profile.Name, EncoderState.Failed, ex.Message, _lastConnected));

                        if (_profile.Protocol == EncoderProtocol.Icecast && IsMountInUseError(ex))
                        {
                            var mountReleased = await TryForceReleaseMountAsync(_cancellationToken).ConfigureAwait(false);
                            if (mountReleased)
                            {
                                _logger.LogWarning("Encoder '{Name}' detected stale mount '{Mount}'. Requested server to drop it and will retry immediately.",
                                    _profile.Name,
                                    NormalizeMount(_profile.Mount));
                                continue;
                            }
                        }

                        _logger.LogWarning(ex,
                            "Encoder '{Encoder}' connection failed ({Host}:{Port}); retrying in {DelaySeconds}s",
                            _profile.Name,
                            _profile.Host,
                            _profile.Port,
                            Math.Min(_backoffSeconds, MaxBackoffSeconds));
                        var delay = TimeSpan.FromSeconds(Math.Min(_backoffSeconds, MaxBackoffSeconds));
                        _backoffSeconds = Math.Min(_backoffSeconds * 2, MaxBackoffSeconds);
                        try
                        {
                            await Task.Delay(delay, _cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        if (encoder != null && stream != null)
                        {
                            encoder.Flush();
                            stream.Flush();
                        }

                        _acceptFrames = false;
                        encoder?.Dispose();
                        stream?.Dispose();
                        client?.Dispose();
                    }
                }

                _acceptFrames = false;
                DrainChannel();
                _owner.PublishStatus(new EncoderStatus(_profile.Id, _profile.Name, EncoderState.Stopped, "Stopped", _lastConnected));
            }

            private async Task<(TcpClient client, Stream stream)> CreateNetworkStreamAsync(CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(_profile.Host))
                {
                    throw new InvalidOperationException("Encoder profile host is not configured.");
                }

                var client = new TcpClient { NoDelay = true };
                await client.ConnectAsync(_profile.Host, _profile.Port, cancellationToken).ConfigureAwait(false);
                var networkStream = client.GetStream();
                Stream writeStream = networkStream;

                if (_profile.UseSsl)
                {
                    var ssl = new SslStream(networkStream, leaveInnerStreamOpen: false);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = _profile.Host,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, cancellationToken).ConfigureAwait(false);
                    writeStream = ssl;
                }

                await SendHandshakeAsync(writeStream, cancellationToken).ConfigureAwait(false);
                var buffered = new BufferedStream(writeStream, 16 * 1024);
                return (client, buffered);
            }

            private async Task SendHandshakeAsync(Stream stream, CancellationToken cancellationToken)
            {
                if (_profile.Protocol == EncoderProtocol.Shoutcast)
                {
                    await SendShoutcastHandshakeAsync(stream, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendIcecastHandshakeAsync(stream, cancellationToken).ConfigureAwait(false);
                }
            }

            private async Task SendIcecastHandshakeAsync(Stream stream, CancellationToken cancellationToken)
            {
                var request = BuildIcecastSourceRequest();
                var buffer = Encoding.ASCII.GetBytes(request);
                await stream.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await ValidateResponseAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            private async Task SendShoutcastHandshakeAsync(Stream stream, CancellationToken cancellationToken)
            {
                var password = string.IsNullOrWhiteSpace(_profile.Password) ? string.Empty : _profile.Password.Trim();
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException("Shoutcast profiles require a password.");
                }

                var passwordLine = Encoding.ASCII.GetBytes(password + "\r\n");
                await stream.WriteAsync(passwordLine.AsMemory(0, passwordLine.Length), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                var response = await ReadAsciiLineAsync(stream, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Encoder '{Name}' shoutcast password response: {Response}", _profile.Name, response ?? "<no response>");
                if (!string.Equals(response, "OK2", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Shoutcast server rejected password ({response ?? "no response"})");
                }

                var metadata = BuildShoutcastMetadataHeaders();
                await stream.WriteAsync(metadata.AsMemory(0, metadata.Length), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                response = await ReadAsciiLineAsync(stream, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Encoder '{Name}' shoutcast metadata response: {Response}", _profile.Name, response ?? "<no response>");
                if (!string.Equals(response, "OK2", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Shoutcast server rejected metadata ({response ?? "no response"})");
                }
            }

            private async Task TrySendMetadataAsync(EncoderMetadata? metadata, CancellationToken cancellationToken)
            {
                if (metadata == null)
                {
                    return;
                }

                var payload = metadata.BuildSongValue();
                if (string.IsNullOrWhiteSpace(payload) || string.Equals(_lastMetadataPayload, payload, StringComparison.Ordinal))
                {
                    return;
                }

                try
                {
                    if (_profile.Protocol == EncoderProtocol.Icecast)
                    {
                        await SendIcecastMetadataUpdateAsync(payload, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendShoutcastMetadataUpdateAsync(payload, cancellationToken).ConfigureAwait(false);
                    }

                    _lastMetadataPayload = payload;
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Encoder '{Name}' metadata update failed", _profile.Name);
                }
            }

            private async Task SendIcecastMetadataUpdateAsync(string payload, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(_profile.Host))
                {
                    return;
                }

                if (!TryResolveIcecastCredentials(out var user, out var password))
                {
                    _logger.LogWarning("Encoder '{Name}' cannot send Icecast metadata because no credentials are configured.", _profile.Name);
                    return;
                }

                using var client = new TcpClient { NoDelay = true };
                await client.ConnectAsync(_profile.Host, _profile.Port, cancellationToken).ConfigureAwait(false);
                Stream adminStream = client.GetStream();
                if (_profile.UseSsl)
                {
                    var ssl = new SslStream(adminStream, leaveInnerStreamOpen: false);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = _profile.Host,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, cancellationToken).ConfigureAwait(false);
                    adminStream = ssl;
                }

                var path = BuildIcecastMetadataPath(payload);
                var builder = new StringBuilder();
                builder.Append("GET ").Append(path).Append(" HTTP/1.0\r\n");
                builder.Append("Host: ").Append(string.IsNullOrWhiteSpace(_profile.Host) ? "localhost" : _profile.Host.Trim()).Append("\r\n");
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}"));
                builder.Append("Authorization: Basic ").Append(credentials).Append("\r\n");
                builder.Append("User-Agent: OpenBroadcaster/1.0\r\n\r\n");
                var buffer = Encoding.ASCII.GetBytes(builder.ToString());
                await adminStream.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                await adminStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                var responseLine = await ReadAsciiLineAsync(adminStream, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Encoder '{Name}' Icecast metadata response: {Response}", _profile.Name, responseLine ?? "<no response>");
            }

            private async Task SendShoutcastMetadataUpdateAsync(string payload, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(_profile.Host))
                {
                    return;
                }

                var adminPassword = string.IsNullOrWhiteSpace(_profile.AdminPassword) ? _profile.Password : _profile.AdminPassword;
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    _logger.LogDebug("Skipping Shoutcast metadata update for '{Name}' because no admin password is configured.", _profile.Name);
                    return;
                }

                using var client = new TcpClient { NoDelay = true };
                await client.ConnectAsync(_profile.Host, _profile.Port, cancellationToken).ConfigureAwait(false);
                Stream adminStream = client.GetStream();
                if (_profile.UseSsl)
                {
                    var ssl = new SslStream(adminStream, leaveInnerStreamOpen: false);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = _profile.Host,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, cancellationToken).ConfigureAwait(false);
                    adminStream = ssl;
                }

                var path = BuildShoutcastMetadataPath(adminPassword, payload);
                var builder = new StringBuilder();
                builder.Append("GET ").Append(path).Append(" HTTP/1.0\r\n");
                builder.Append("Host: ").Append(string.IsNullOrWhiteSpace(_profile.Host) ? "localhost" : _profile.Host.Trim()).Append("\r\n");
                builder.Append("User-Agent: OpenBroadcaster/1.0\r\n\r\n");
                var buffer = Encoding.ASCII.GetBytes(builder.ToString());
                await adminStream.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                await adminStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                var responseLine = await ReadAsciiLineAsync(adminStream, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Encoder '{Name}' Shoutcast metadata response: {Response}", _profile.Name, responseLine ?? "<no response>");
            }

            private string BuildIcecastMetadataPath(string payload)
            {
                var mount = NormalizeMount(_profile.Mount);
                return $"/admin/metadata?mode=updinfo&mount={Uri.EscapeDataString(mount)}&song={Uri.EscapeDataString(payload)}";
            }

            private string BuildShoutcastMetadataPath(string adminPassword, string payload)
            {
                var song = Uri.EscapeDataString(payload);
                var password = Uri.EscapeDataString(adminPassword);
                var url = Uri.EscapeDataString(string.IsNullOrWhiteSpace(_profile.Mount) ? "/" : _profile.Mount.Trim());
                return $"/admin.cgi?mode=updinfo&pass={password}&song={song}&url={url}";
            }

            private bool TryResolveIcecastCredentials(out string user, out string password)
            {
                if (!string.IsNullOrWhiteSpace(_profile.AdminUser) && !string.IsNullOrWhiteSpace(_profile.AdminPassword))
                {
                    user = _profile.AdminUser.Trim();
                    password = _profile.AdminPassword;
                    return true;
                }

                var fallbackUser = string.IsNullOrWhiteSpace(_profile.Username) ? "source" : _profile.Username.Trim();
                var fallbackPassword = _profile.Password ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(fallbackPassword))
                {
                    user = fallbackUser;
                    password = fallbackPassword;
                    return true;
                }

                user = string.Empty;
                password = string.Empty;
                return false;
            }

            private async Task<string?> ReadAsciiLineAsync(Stream stream, CancellationToken cancellationToken)
            {
                var builder = new StringBuilder();
                var buffer = new byte[1];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return builder.Length == 0 ? null : builder.ToString();
                    }

                    var ch = (char)buffer[0];
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (ch != '\r')
                    {
                        builder.Append(ch);
                    }
                }

                return builder.ToString();
            }

            private static bool IsMountInUseError(Exception ex)
            {
                var message = ex.Message ?? string.Empty;
                return message.IndexOf("mount", StringComparison.OrdinalIgnoreCase) >= 0
                    && message.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private async Task<bool> TryForceReleaseMountAsync(CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(_profile.Host))
                {
                    return false;
                }

                if (!TryResolveIcecastCredentials(out var user, out var password))
                {
                    _logger.LogWarning("Encoder '{Name}' cannot request mount release because no admin/source credentials are configured.", _profile.Name);
                    return false;
                }

                var mount = NormalizeMount(_profile.Mount);
                try
                {
                    using var client = new TcpClient { NoDelay = true };
                    await client.ConnectAsync(_profile.Host, _profile.Port, cancellationToken).ConfigureAwait(false);
                    Stream adminStream = client.GetStream();
                    if (_profile.UseSsl)
                    {
                        var ssl = new SslStream(adminStream, leaveInnerStreamOpen: false);
                        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                        {
                            TargetHost = _profile.Host,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                        }, cancellationToken).ConfigureAwait(false);
                        adminStream = ssl;
                    }

                    var request = BuildAdminKickRequest(mount, user, password);
                    var buffer = Encoding.ASCII.GetBytes(request);
                    await adminStream.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    await adminStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    var responseLine = await ReadAsciiLineAsync(adminStream, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Encoder '{Name}' admin kick response: {Response}", _profile.Name, responseLine ?? "<no response>");
                    return responseLine != null && responseLine.Contains("200", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception kickEx)
                {
                    _logger.LogWarning(kickEx, "Encoder '{Name}' failed to request mount release", _profile.Name);
                    return false;
                }
            }

            private string BuildAdminKickRequest(string mount, string user, string password)
            {
                var builder = new StringBuilder();
                var escapedMount = Uri.EscapeDataString(mount);
                builder.Append("GET /admin/kick-source?mount=").Append(escapedMount).Append(" HTTP/1.0\r\n");
                builder.Append("Host: ").Append(string.IsNullOrWhiteSpace(_profile.Host) ? "localhost" : _profile.Host.Trim()).Append("\r\n");
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}"));
                builder.Append("Authorization: Basic ").Append(credentials).Append("\r\n");
                builder.Append("User-Agent: OpenBroadcaster/1.0\r\n\r\n");
                return builder.ToString();
            }

            private async Task ValidateResponseAsync(Stream stream, CancellationToken cancellationToken)
            {
                using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                var statusLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Encoder '{Name}' icecast response: {Response}", _profile.Name, statusLine ?? "<no response>");
                if (string.IsNullOrWhiteSpace(statusLine) || !statusLine.Contains("200", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Encoder rejected connection: {statusLine ?? "no response"}");
                }

                string? line;
                do
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                while (!string.IsNullOrEmpty(line));
            }

            private string BuildIcecastSourceRequest()
            {
                var mount = NormalizeMount(_profile.Mount);
                var hostHeader = string.IsNullOrWhiteSpace(_profile.Host) ? "localhost" : _profile.Host.Trim();
                var userName = string.IsNullOrWhiteSpace(_profile.Username) ? "source" : _profile.Username.Trim();
                var password = _profile.Password ?? string.Empty;
                var builder = new StringBuilder();
                builder.Append("SOURCE ").Append(mount).Append(" HTTP/1.1\r\n");
                builder.Append("Host: ").Append(hostHeader).Append(":").Append(_profile.Port).Append("\r\n");
                builder.Append("User-Agent: OpenBroadcaster/1.0\r\n");
                builder.Append("Content-Type: audio/mpeg\r\n");
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"));
                builder.Append("Authorization: Basic ").Append(credentials).Append("\r\n");
                builder.Append("Ice-Name: ").Append(_profile.Name).Append("\r\n");
                if (!string.IsNullOrWhiteSpace(_profile.Description))
                {
                    builder.Append("Ice-Description: ").Append(_profile.Description).Append("\r\n");
                }

                if (!string.IsNullOrWhiteSpace(_profile.Genre))
                {
                    builder.Append("Ice-Genre: ").Append(_profile.Genre).Append("\r\n");
                }

                builder.Append("Ice-Public: ").Append(_profile.Public ? "1" : "0").Append("\r\n");
                builder.Append("Ice-Audio-Info: ice-samplerate=").Append(_waveFormat.SampleRate)
                    .Append(";ice-bitrate=").Append(_profile.BitrateKbps)
                    .Append(";ice-channels=").Append(_waveFormat.Channels)
                    .Append("\r\n");
                builder.Append("Connection: Keep-Alive\r\n\r\n");
                return builder.ToString();
            }

            private byte[] BuildShoutcastMetadataHeaders()
            {
                var builder = new StringBuilder();
                var stationName = string.IsNullOrWhiteSpace(_profile.Name) ? "OpenBroadcaster" : _profile.Name.Trim();
                var genre = string.IsNullOrWhiteSpace(_profile.Genre) ? "Variety" : _profile.Genre.Trim();
                var description = string.IsNullOrWhiteSpace(_profile.Description) ? "OpenBroadcaster stream" : _profile.Description.Trim();
                builder.Append("icy-name: ").Append(stationName).Append("\r\n");
                builder.Append("icy-genre: ").Append(genre).Append("\r\n");
                builder.Append("icy-description: ").Append(description).Append("\r\n");
                builder.Append("icy-url: ").Append(string.IsNullOrWhiteSpace(_profile.Mount) ? "/" : _profile.Mount.Trim()).Append("\r\n");
                builder.Append("icy-pub: ").Append(_profile.Public ? "1" : "0").Append("\r\n");
                builder.Append("icy-br: ").Append(Math.Max(32, _profile.BitrateKbps)).Append("\r\n");
                builder.Append("content-type: audio/mpeg\r\n\r\n");
                return Encoding.ASCII.GetBytes(builder.ToString());
            }

            private static string NormalizeMount(string? mount)
            {
                if (string.IsNullOrWhiteSpace(mount))
                {
                    return "/live";
                }

                var trimmed = mount.Trim();
                return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
            }

            private void DrainChannel()
            {
                while (_channel.Reader.TryRead(out var frame))
                {
                    ArrayPool<byte>.Shared.Return(frame.Buffer);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _acceptFrames = false;
                _channel.Writer.TryComplete();
                try
                {
                    _processingTask?.Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // Ignore shutdown exceptions.
                }
            }

            private sealed class Mp3Encoder : IDisposable
            {
                private readonly Stream _destination;
                private readonly Process? _ffmpegProcess;
                private readonly Stream? _ffmpegStdin;
                private readonly bool _useFfmpeg;

                public Mp3Encoder(WaveFormat sourceFormat, int bitrateKbps, Stream destination)
                {
                    if (sourceFormat.Encoding != WaveFormatEncoding.Pcm || sourceFormat.BitsPerSample != 16)
                    {
                        throw new InvalidOperationException("MP3 encoder requires 16-bit PCM input.");
                    }

                    _destination = destination;
                    var sanitizedBitrate = Math.Max(32, bitrateKbps);

                    // On Linux, use ffmpeg for MP3 encoding; on Windows, use LAME
                    if (OperatingSystem.IsLinux())
                    {
                        _useFfmpeg = true;
                        var psi = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = false,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        psi.ArgumentList.Add("-hide_banner");
                        psi.ArgumentList.Add("-loglevel");
                        psi.ArgumentList.Add("error");
                        psi.ArgumentList.Add("-f");
                        psi.ArgumentList.Add("s16le");
                        psi.ArgumentList.Add("-ar");
                        psi.ArgumentList.Add(sourceFormat.SampleRate.ToString());
                        psi.ArgumentList.Add("-ac");
                        psi.ArgumentList.Add(sourceFormat.Channels.ToString());
                        psi.ArgumentList.Add("-i");
                        psi.ArgumentList.Add("pipe:0");
                        psi.ArgumentList.Add("-c:a");
                        psi.ArgumentList.Add("libmp3lame");
                        psi.ArgumentList.Add("-b:a");
                        psi.ArgumentList.Add($"{sanitizedBitrate}k");
                        psi.ArgumentList.Add("-f");
                        psi.ArgumentList.Add("mp3");
                        psi.ArgumentList.Add("pipe:1");

                        _ffmpegProcess = Process.Start(psi);
                        if (_ffmpegProcess == null)
                        {
                            throw new InvalidOperationException("Failed to start ffmpeg for MP3 encoding.");
                        }

                        _ffmpegStdin = _ffmpegProcess.StandardInput.BaseStream;
                        
                        // Start a thread to copy ffmpeg output to the destination stream
                        var stdout = _ffmpegProcess.StandardOutput.BaseStream;
                        _ = Task.Run(() => CopyOutputAsync(stdout, destination));
                    }
                    else
                    {
                        _useFfmpeg = false;
#if NET8_0_WINDOWS
                        _lameWriter = new LameMP3FileWriter(new NonClosingStreamWrapper(destination), sourceFormat, sanitizedBitrate);
#else
                        throw new PlatformNotSupportedException("MP3 encoding requires ffmpeg on this platform.");
#endif
                    }
                }

#if NET8_0_WINDOWS
                private readonly LameMP3FileWriter? _lameWriter;
#endif

                private static async Task CopyOutputAsync(Stream source, Stream destination)
                {
                    try
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            await destination.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                            await destination.FlushAsync().ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // Ignore errors during shutdown
                    }
                }

                public void Encode(byte[] buffer, int length)
                {
                    if (_useFfmpeg)
                    {
                        try
                        {
                            _ffmpegStdin?.Write(buffer, 0, length);
                        }
                        catch
                        {
                            // Process may have terminated
                        }
                    }
#if NET8_0_WINDOWS
                    else
                    {
                        _lameWriter?.Write(buffer, 0, length);
                    }
#endif
                }

                public void Flush()
                {
                    if (_useFfmpeg)
                    {
                        try
                        {
                            _ffmpegStdin?.Flush();
                        }
                        catch
                        {
                            // Ignore
                        }
                    }
#if NET8_0_WINDOWS
                    else
                    {
                        _lameWriter?.Flush();
                    }
#endif
                }

                public void Dispose()
                {
                    if (_useFfmpeg)
                    {
                        try
                        {
                            _ffmpegStdin?.Close();
                        }
                        catch { }

                        try
                        {
                            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                            {
                                _ffmpegProcess.WaitForExit(2000);
                                if (!_ffmpegProcess.HasExited)
                                {
                                    _ffmpegProcess.Kill();
                                }
                            }
                        }
                        catch { }

                        _ffmpegProcess?.Dispose();
                    }
#if NET8_0_WINDOWS
                    else
                    {
                        _lameWriter?.Dispose();
                    }
#endif
                }
            }
        }

        private readonly struct EncoderBuffer
        {
            public EncoderBuffer(byte[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }

            public byte[] Buffer { get; }
            public int Length { get; }
        }

        private sealed record EncoderMetadata(string Title, string Artist, string Album)
        {
            public string BuildSongValue()
            {
                var resolvedTitle = string.IsNullOrWhiteSpace(Title) ? "OpenBroadcaster" : Title.Trim();
                if (string.IsNullOrWhiteSpace(Artist))
                {
                    return resolvedTitle;
                }

                return $"{Artist.Trim()} - {resolvedTitle}";
            }
        }
    }
}
