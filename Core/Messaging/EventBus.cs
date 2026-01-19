using System;
using System.Collections.Generic;

namespace OpenBroadcaster.Core.Messaging
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly object _sync = new();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);
            lock (_sync)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _handlers[eventType] = handlers;
                }

                handlers.Add(handler);
            }

            return new Subscription(() => Unsubscribe(eventType, handler));
        }

        public void Publish<TEvent>(TEvent eventPayload) where TEvent : class
        {
            if (eventPayload == null)
            {
                throw new ArgumentNullException(nameof(eventPayload));
            }

            List<Delegate>? snapshot;
            lock (_sync)
            {
                _handlers.TryGetValue(typeof(TEvent), out snapshot);
                snapshot = snapshot != null ? new List<Delegate>(snapshot) : null;
            }

            if (snapshot == null)
            {
                return;
            }

            foreach (var handler in snapshot)
            {
                if (handler is Action<TEvent> typed)
                {
                    typed(eventPayload);
                }
            }
        }

        private void Unsubscribe(Type eventType, Delegate handler)
        {
            lock (_sync)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    return;
                }

                handlers.Remove(handler);

                if (handlers.Count == 0)
                {
                    _handlers.Remove(eventType);
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _disposeAction;
            private bool _isDisposed;

            public Subscription(Action disposeAction)
            {
                _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _disposeAction();
                _isDisposed = true;
            }
        }
    }
}
