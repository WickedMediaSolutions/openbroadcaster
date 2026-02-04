using System;
using Xunit;
using OpenBroadcaster.Core.DependencyInjection;

namespace OpenBroadcaster.Tests.Infrastructure
{
    public class ServiceContainerTests
    {
        private interface ITestService { string GetValue(); }
        private class TestService : ITestService
        {
            public string Value { get; set; } = "Test";
            public string GetValue() => Value;
        }

        [Fact]
        public void RegisterSingleton_ReturnsTheSameInstance()
        {
            // Arrange
            var container = new ServiceContainer();
            container.RegisterSingleton<ITestService>(() => new TestService());

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.NotNull(instance1);
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void RegisterSingleton_WithInstance_ReturnsTheInstance()
        {
            // Arrange
            var container = new ServiceContainer();
            var expectedInstance = new TestService { Value = "Expected" };
            container.RegisterSingleton<ITestService>(expectedInstance);

            // Act
            var actualInstance = container.Resolve<ITestService>();

            // Assert
            Assert.Same(expectedInstance, actualInstance);
        }

        [Fact]
        public void RegisterTransient_ReturnsDifferentInstances()
        {
            // Arrange
            var container = new ServiceContainer();
            container.RegisterTransient<ITestService>(() => new TestService());

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.NotSame(instance1, instance2);
        }

        [Fact]
        public void Resolve_UnregisteredService_ThrowsException()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
        }

        [Fact]
        public void IsRegistered_RegisteredService_ReturnsTrue()
        {
            // Arrange
            var container = new ServiceContainer();
            container.RegisterSingleton<ITestService>(() => new TestService());

            // Act
            var isRegistered = container.IsRegistered<ITestService>();

            // Assert
            Assert.True(isRegistered);
        }

        [Fact]
        public void IsRegistered_UnregisteredService_ReturnsFalse()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act
            var isRegistered = container.IsRegistered<ITestService>();

            // Assert
            Assert.False(isRegistered);
        }

        [Fact]
        public void Clear_RemovesAllRegistrations()
        {
            // Arrange
            var container = new ServiceContainer();
            container.RegisterSingleton<ITestService>(() => new TestService());

            // Act
            container.Clear();
            var isRegistered = container.IsRegistered<ITestService>();

            // Assert
            Assert.False(isRegistered);
        }
    }
}
