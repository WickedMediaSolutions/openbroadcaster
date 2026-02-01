using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class TwitchIrcClient : ITwitchIrcClient
    {
        private const string Host = "irc.chat.twitch.tv";
        private const int Port = 6697; // SSL port

        private TcpClient? _tcpClient;
        private SslStream? _sslStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private TwitchChatOptions? _options;
        private readonly ILogger<TwitchIrcClient> _logger;

        public event EventHandler<TwitchChatMessage>? MessageReceived;
        public event EventHandler<string>? NoticeReceived;
        public event EventHandler? ConnectionClosed;

        public bool IsConnected => _tcpClient?.Connected == true;

        public TwitchIrcClient(ILogger<TwitchIrcClient>? logger = null)
        {
            _logger = logger ?? AppLogger.CreateLogger<TwitchIrcClient>();
        }

        public async Task ConnectAsync(TwitchChatOptions options, CancellationToken token)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.IsValid)
            {
                throw new ArgumentException("Twitch chat options are invalid.", nameof(options));
            }

            if (IsConnected)
            {
                throw new InvalidOperationException("Twitch IRC client is already connected.");
            }

            _options = options;
            _logger.LogInformation("Establishing Twitch IRC connection to {Channel} via SSL (user: {User})", options.NormalizedChannel, options.UserName);

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(Host, Port, token).ConfigureAwait(false);
            _logger.LogInformation("TCP connected to {Host}:{Port}", Host, Port);
            
            // Wrap the network stream with SSL
            _sslStream = new SslStream(_tcpClient.GetStream(), false);
            await _sslStream.AuthenticateAsClientAsync(Host).ConfigureAwait(false);
            _logger.LogInformation("SSL handshake complete");
            
            // Use UTF8 encoding WITHOUT BOM - Twitch IRC rejects the BOM character
            var utf8NoBom = new System.Text.UTF8Encoding(false);
            _reader = new StreamReader(_sslStream, utf8NoBom);
            _writer = new StreamWriter(_sslStream, utf8NoBom) { NewLine = "\r\n", AutoFlush = true };

            // Twitch IRC requires lowercase username for NICK
            var nick = options.UserName.ToLowerInvariant();
            
            // Log detailed token info for debugging
            var hasOAuthPrefix = options.OAuthToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase);
            var tokenLength = options.OAuthToken.Length;
            var tokenWithoutPrefix = hasOAuthPrefix ? options.OAuthToken.Substring(6) : options.OAuthToken;
            _logger.LogInformation("Token debug: length={Length}, hasOAuthPrefix={HasPrefix}, rawTokenLength={RawLen}", 
                tokenLength, hasOAuthPrefix, tokenWithoutPrefix.Length);
            _logger.LogInformation("Sending auth - NICK={Nick}, Channel={Channel}", nick, options.ChannelWithHash);
            
            // Send PASS first with the oauth token
            var passCmd = $"PASS {options.OAuthToken}";
            _logger.LogWarning("DEBUG PASS command bytes: {Bytes}", BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(passCmd)));
            _logger.LogInformation("Sending: PASS oauth:***{LastChars}", tokenWithoutPrefix.Length > 4 ? tokenWithoutPrefix.Substring(tokenWithoutPrefix.Length - 4) : "****");
            await _writer.WriteLineAsync(passCmd).ConfigureAwait(false);
            
            // Send NICK
            _logger.LogInformation("Sending: NICK {Nick}", nick);
            await _writer.WriteLineAsync($"NICK {nick}").ConfigureAwait(false);

            // Start listener immediately to catch auth response
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
            
            // Wait briefly for auth to complete before joining
            await Task.Delay(1000, token).ConfigureAwait(false);
            
            _logger.LogInformation("Joining channel {Channel}", options.ChannelWithHash);
            await _writer.WriteLineAsync($"JOIN {options.ChannelWithHash}").ConfigureAwait(false);
        }

        public async Task DisconnectAsync()
        {
            if (_cts == null)
            {
                Cleanup();
                return;
            }

            try
            {
                _cts.Cancel();
                if (_listenerTask != null)
                {
                    await _listenerTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _logger.LogInformation("Twitch IRC disconnected");
                Cleanup();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || _writer == null || _options == null)
            {
                return;
            }

            await SendRawAsync($"PRIVMSG {_options.ChannelWithHash} :{message}").ConfigureAwait(false);
            _logger.LogDebug("Sent PRIVMSG payload ({Length} chars)", message.Length);
        }

        private async Task SendRawAsync(string payload)
        {
            if (_writer == null)
            {
                return;
            }

            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(payload).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _reader != null)
                {
                    var line = await _reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    _logger.LogInformation("Twitch IRC received: {Line}", line);

                    // Check for auth failure - Twitch sends various error formats
                    if (line.Contains("Login authentication failed", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Login unsuccessful", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Improperly formatted auth", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Error logging in", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("Twitch authentication failed: {Line}", line);
                        NoticeReceived?.Invoke(this, $"AUTH FAILED: {line}");
                        break;
                    }
                    
                    // Check for successful login
                    if (line.Contains(":tmi.twitch.tv 001", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Welcome, GLHF!", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Twitch authentication successful!");
                        NoticeReceived?.Invoke(this, "Connected to Twitch IRC!");
                    }

                    if (line.StartsWith("PING", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendRawAsync(line.Replace("PING", "PONG", StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
                        _logger.LogDebug("Responded to Twitch PING");
                        continue;
                    }

                    if (line.Contains("PRIVMSG", StringComparison.Ordinal))
                    {
                        var parsed = ParsePrivMsg(line);
                        if (parsed != null)
                        {
                            MessageReceived?.Invoke(this, parsed);
                            _logger.LogDebug("Received message from {User}", parsed.UserName);
                        }
                        continue;
                    }

                    NoticeReceived?.Invoke(this, line);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                NoticeReceived?.Invoke(this, $"Twitch IRC error: {ex.Message}");
                _logger.LogError(ex, "Twitch IRC listener faulted");
            }
            finally
            {
                ConnectionClosed?.Invoke(this, EventArgs.Empty);
                _logger.LogWarning("Twitch IRC listener terminated");
                Cleanup();
            }
        }

        private TwitchChatMessage? ParsePrivMsg(string raw)
        {
            if (_options == null || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var userEnd = raw.IndexOf('!');
            if (userEnd <= 1)
            {
                return null;
            }

            var userName = raw[1..userEnd];
            var payloadIndex = raw.IndexOf(" :", StringComparison.Ordinal);
            if (payloadIndex < 0 || payloadIndex + 2 > raw.Length)
            {
                return null;
            }

            var message = raw[(payloadIndex + 2)..];
            var isBroadcaster = string.Equals(userName, _options.NormalizedChannel, StringComparison.OrdinalIgnoreCase);
            var isFromBot = string.Equals(userName, _options.UserName, StringComparison.OrdinalIgnoreCase);
            return new TwitchChatMessage(userName, message, DateTime.UtcNow, false, isBroadcaster, isFromBot);
        }

        private void Cleanup()
        {
            _cts?.Dispose();
            _cts = null;

            _reader?.Dispose();
            _reader = null;

            _writer?.Dispose();
            _writer = null;

            _sslStream?.Dispose();
            _sslStream = null;

            _tcpClient?.Dispose();
            _tcpClient = null;

            _listenerTask = null;
        }

        public void Dispose()
        {
            _sendLock.Dispose();
            _ = DisconnectAsync();
            _logger.LogInformation("TwitchIrcClient disposed");
        }
    }
}
