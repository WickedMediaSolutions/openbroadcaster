using System;

namespace OpenBroadcaster.Core.Relay.Client
{
    /// <summary>
    /// Simple logging interface for the relay client.
    /// 
    /// DESIGN RATIONALE:
    /// - Abstraction allows integration with any logging framework (Serilog, NLog, etc.)
    /// - Does not assume Microsoft.Extensions.Logging dependency
    /// - Implementers can filter by level and category
    /// </summary>
    public interface IRelayClientLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception? exception = null);
    }

    /// <summary>
    /// No-op logger implementation for when logging is not needed.
    /// </summary>
    public sealed class NullRelayClientLogger : IRelayClientLogger
    {
        public static readonly NullRelayClientLogger Instance = new();

        private NullRelayClientLogger() { }

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    /// <summary>
    /// Console logger for debugging.
    /// </summary>
    public sealed class ConsoleRelayClientLogger : IRelayClientLogger
    {
        private readonly string _category;

        public ConsoleRelayClientLogger(string category = "RelayClient")
        {
            _category = category;
        }

        public void Debug(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] [{_category}] {message}");
        }

        public void Info(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [INFO ] [{_category}] {message}");
        }

        public void Warning(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [WARN ] [{_category}] {message}");
        }

        public void Error(string message, Exception? exception = null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] [{_category}] {message}");
            if (exception != null)
            {
                Console.WriteLine($"  Exception: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
