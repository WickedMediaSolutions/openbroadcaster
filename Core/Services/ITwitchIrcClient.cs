using System;
using System.Threading;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public interface ITwitchIrcClient : IDisposable
    {
        event EventHandler<TwitchChatMessage>? MessageReceived;
        event EventHandler<string>? NoticeReceived;
        event EventHandler? ConnectionClosed;

        bool IsConnected { get; }

        Task ConnectAsync(TwitchChatOptions options, CancellationToken token);
        Task DisconnectAsync();
        Task SendMessageAsync(string message);
    }
}