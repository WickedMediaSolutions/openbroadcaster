using System;
using System.IO;

namespace OpenBroadcaster.Core.Logging
{
    /// <summary>
    /// File-based logger implementation.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();

        public FileLogger(string logFilePath, LogLevel minLevel = LogLevel.Information)
        {
            _logFilePath = logFilePath;
            _minLevel = minLevel;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level < _minLevel)
            {
                return;
            }

            var timestamp = DateTime.UtcNow.ToString("o");
            var levelStr = level.ToString().ToUpper();
            var logMessage = $"[{timestamp}] [{levelStr}] {message}";

            if (exception != null)
            {
                logMessage += $"\n  Exception: {exception.GetType().Name}: {exception.Message}\n  {exception.StackTrace}";
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logMessage + "\n");
                }
                catch
                {
                    // Swallow logging errors to prevent cascading failures
                }
            }

            // Also write to debug output
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        public void LogTrace(string message) => Log(LogLevel.Trace, message);
        public void LogDebug(string message) => Log(LogLevel.Debug, message);
        public void LogInformation(string message) => Log(LogLevel.Information, message);
        public void LogWarning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);
        public void LogError(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
        public void LogCritical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);
    }

    /// <summary>
    /// Generic file logger with category.
    /// </summary>
    public class FileLogger<T> : FileLogger, ILogger<T>
    {
        public string Category { get; }

        public FileLogger(string logFilePath, LogLevel minLevel = LogLevel.Information)
            : base(logFilePath, minLevel)
        {
            Category = typeof(T).Name;
        }
    }
}
