using System;

namespace OpenBroadcaster.Core.Messaging
{
    public interface IEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
        void Publish<TEvent>(TEvent eventPayload) where TEvent : class;
    }
}
