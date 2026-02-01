using System;
using System.Threading;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Tools.Fakes
{
    public sealed class FakeTwitchIrcClient : OpenBroadcaster.Core.Services.ITwitchIrcClient
    {
        private bool _connected;
        public bool IsConnected => _connected;

        public async Task ConnectAsync(TwitchChatOptions options, CancellationToken token)
        {
            _connected = true;
            await Task.Delay(50, token).ConfigureAwait(false);
            NoticeReceived?.Invoke(this, "Connected (fake)");
        }

        public async Task DisconnectAsync()
        {
            _connected = false;
            await Task.Delay(10).ConfigureAwait(false);
            ConnectionClosed?.Invoke(this, EventArgs.Empty);
        }

        public Task SendMessageAsync(string message)
        {
            // no-op for fake
            return Task.CompletedTask;
        }

        public event EventHandler<TwitchChatMessage>? MessageReceived;
        public event EventHandler<string>? NoticeReceived;
        public event EventHandler? ConnectionClosed;

        public void Dispose()
        {
        }
    }
}
