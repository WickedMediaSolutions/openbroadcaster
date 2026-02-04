using System;
using System.IO;

namespace OpenBroadcaster.Core.Logging
{
    /// <summary>
    /// Factory for creating loggers.
    /// </summary>
    public class LoggerFactory
    {
        private static LoggerFactory? _instance;
        private static readonly object _lock = new object();

        private string _logDirectory;
        private LogLevel _minLevel;

        private LoggerFactory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appData, "OpenBroadcaster", "logs");
            _minLevel = LogLevel.Information;
        }

        /// <summary>
        /// Gets the singleton instance of the logger factory.
        /// </summary>
        public static LoggerFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LoggerFactory();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Configures the logger factory.
        /// </summary>
        public void Configure(string logDirectory, LogLevel minLevel = LogLevel.Information)
        {
            _logDirectory = logDirectory;
            _minLevel = minLevel;
        }

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        public ILogger<T> CreateLogger<T>()
        {
            var fileName = $"{typeof(T).Name}.log";
            var filePath = Path.Combine(_logDirectory, fileName);
            return new FileLogger<T>(filePath, _minLevel);
        }

        /// <summary>
        /// Creates a logger with the specified category name.
        /// </summary>
        public ILogger CreateLogger(string categoryName)
        {
            var fileName = $"{categoryName}.log";
            var filePath = Path.Combine(_logDirectory, fileName);
            return new FileLogger(filePath, _minLevel);
        }
    }
}
