using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace OpenBroadcaster.Core.Diagnostics
{
    public static class AppLogger
    {
        private static readonly object SyncRoot = new();
        private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        private static Serilog.ILogger? _serilogLogger;
        private static bool _isConfigured;
        private static string _logDirectory = string.Empty;
        private static string? _currentLogFile;

        public static void Configure(string? logDirectory = null)
        {
            lock (SyncRoot)
            {
                if (_isConfigured)
                {
                    return;
                }

                var baseDirectory = logDirectory ?? ResolveDefaultDirectory();
                Directory.CreateDirectory(baseDirectory);
                var sessionFile = Path.Combine(baseDirectory, $"openbroadcaster-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

                _serilogLogger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        path: sessionFile,
                        rollingInterval: RollingInterval.Infinite,
                        retainedFileCountLimit: 30,
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(5))
                    .CreateLogger();

                _loggerFactory = new SerilogLoggerFactory(_serilogLogger, dispose: true);
                _logDirectory = baseDirectory;
                _currentLogFile = sessionFile;
                _isConfigured = true;
            }
        }

        public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }

        public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_isConfigured)
                {
                    return;
                }

                (_loggerFactory as IDisposable)?.Dispose();
                _loggerFactory = NullLoggerFactory.Instance;
                _serilogLogger = null;
                Log.CloseAndFlush();
                _isConfigured = false;
                _logDirectory = string.Empty;
                _currentLogFile = null;
            }
        }

        public static string LogDirectory => string.IsNullOrWhiteSpace(_logDirectory) ? ResolveDefaultDirectory() : _logDirectory;

        public static string? CurrentLogFile => _currentLogFile;

        private static string ResolveDefaultDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "OpenBroadcaster", "logs");
        }
    }
}
