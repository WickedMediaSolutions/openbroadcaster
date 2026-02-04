using System;

namespace OpenBroadcaster.Core.Logging
{
    /// <summary>
    /// Log levels for structured logging.
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Structured logging interface for OpenBroadcaster.
    /// </summary>
    public interface ILogger
    {
        void Log(LogLevel level, string message, Exception? exception = null);
        void LogTrace(string message);
        void LogDebug(string message);
        void LogInformation(string message);
        void LogWarning(string message, Exception? exception = null);
        void LogError(string message, Exception? exception = null);
        void LogCritical(string message, Exception? exception = null);
    }

    /// <summary>
    /// Generic logger interface with category.
    /// </summary>
    public interface ILogger<T> : ILogger
    {
        string Category { get; }
    }
}
