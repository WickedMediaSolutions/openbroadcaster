using System;
using System.IO;
using Xunit;
using OpenBroadcaster.Core.Logging;

namespace OpenBroadcaster.Tests.Infrastructure
{
    public class LoggerTests : IDisposable
    {
        private readonly string _testLogPath;

        public LoggerTests()
        {
            _testLogPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.log");
        }

        public void Dispose()
        {
            if (File.Exists(_testLogPath))
            {
                File.Delete(_testLogPath);
            }
        }

        [Fact]
        public void LogInformation_WritesToFile()
        {
            // Arrange
            var logger = new FileLogger(_testLogPath, LogLevel.Information);
            var message = "Test information message";

            // Act
            logger.LogInformation(message);

            // Assert
            var content = File.ReadAllText(_testLogPath);
            Assert.Contains(message, content);
            Assert.Contains("[INFORMATION]", content);
        }

        [Fact]
        public void LogError_WithException_IncludesExceptionDetails()
        {
            // Arrange
            var logger = new FileLogger(_testLogPath, LogLevel.Error);
            var exception = new InvalidOperationException("Test exception");

            // Act
            logger.LogError("Error occurred", exception);

            // Assert
            var content = File.ReadAllText(_testLogPath);
            Assert.Contains("Error occurred", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("Test exception", content);
        }

        [Fact]
        public void Log_BelowMinLevel_DoesNotWrite()
        {
            // Arrange
            var logger = new FileLogger(_testLogPath, LogLevel.Warning);

            // Act
            logger.LogInformation("This should not be logged");

            // Assert
            var fileExists = File.Exists(_testLogPath);
            Assert.False(fileExists || new FileInfo(_testLogPath).Length == 0);
        }

        [Fact]
        public void LogCritical_WritesWithCriticalLevel()
        {
            // Arrange
            var logger = new FileLogger(_testLogPath, LogLevel.Critical);
            var message = "Critical error";

            // Act
            logger.LogCritical(message);

            // Assert
            var content = File.ReadAllText(_testLogPath);
            Assert.Contains(message, content);
            Assert.Contains("[CRITICAL]", content);
        }

        [Fact]
        public void GenericLogger_HasCorrectCategory()
        {
            // Arrange
            var logger = new FileLogger<LoggerTests>(_testLogPath);

            // Act
            var category = logger.Category;

            // Assert
            Assert.Equal("LoggerTests", category);
        }

        [Fact]
        public void LoggerFactory_CreateLogger_CreatesValidLogger()
        {
            // Arrange
            var factory = LoggerFactory.Instance;
            var tempDir = Path.Combine(Path.GetTempPath(), $"logs_{Guid.NewGuid()}");
            factory.Configure(tempDir, LogLevel.Debug);

            // Act
            var logger = factory.CreateLogger<LoggerTests>();

            // Assert
            Assert.NotNull(logger);
            Assert.Equal("LoggerTests", logger.Category);

            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
