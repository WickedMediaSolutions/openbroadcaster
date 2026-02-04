using System;
using System.Collections.Generic;

namespace OpenBroadcaster.Core.DependencyInjection
{
    /// <summary>
    /// Simple dependency injection container for OpenBroadcaster.
    /// Supports singleton and transient registrations.
    /// </summary>
    public class ServiceContainer : IServiceProvider
    {
        private readonly Dictionary<Type, Func<object>> _singletonFactories = new();
        private readonly Dictionary<Type, object> _singletonInstances = new();
        private readonly Dictionary<Type, Func<object>> _transientFactories = new();
        private readonly object _lock = new object();

        /// <summary>
        /// Registers a singleton service. Only one instance will be created.
        /// </summary>
        public void RegisterSingleton<TService>(Func<TService> factory) where TService : class
        {
            lock (_lock)
            {
                _singletonFactories[typeof(TService)] = () => factory();
            }
        }

        /// <summary>
        /// Registers a singleton instance directly.
        /// </summary>
        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            lock (_lock)
            {
                _singletonInstances[typeof(TService)] = instance;
            }
        }

        /// <summary>
        /// Registers a transient service. A new instance is created each time.
        /// </summary>
        public void RegisterTransient<TService>(Func<TService> factory) where TService : class
        {
            lock (_lock)
            {
                _transientFactories[typeof(TService)] = () => factory();
            }
        }

        /// <summary>
        /// Resolves a service from the container.
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return (T)GetService(typeof(T))!;
        }

        /// <summary>
        /// Gets a service from the container.
        /// </summary>
        public object? GetService(Type serviceType)
        {
            lock (_lock)
            {
                // Check for existing singleton instance
                if (_singletonInstances.TryGetValue(serviceType, out var instance))
                {
                    return instance;
                }

                // Create singleton if factory exists
                if (_singletonFactories.TryGetValue(serviceType, out var singletonFactory))
                {
                    instance = singletonFactory();
                    _singletonInstances[serviceType] = instance;
                    return instance;
                }

                // Create transient if factory exists
                if (_transientFactories.TryGetValue(serviceType, out var transientFactory))
                {
                    return transientFactory();
                }

                throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered.");
            }
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        public bool IsRegistered<TService>()
        {
            return IsRegistered(typeof(TService));
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        public bool IsRegistered(Type serviceType)
        {
            lock (_lock)
            {
                return _singletonInstances.ContainsKey(serviceType) ||
                       _singletonFactories.ContainsKey(serviceType) ||
                       _transientFactories.ContainsKey(serviceType);
            }
        }

        /// <summary>
        /// Clears all registrations (useful for testing).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _singletonInstances.Clear();
                _singletonFactories.Clear();
                _transientFactories.Clear();
            }
        }
    }
}
