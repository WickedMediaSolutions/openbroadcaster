using System;
using Xunit;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Tests.Infrastructure
{
    public class TokenProtectionTests
    {
        [Fact]
        public void Protect_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var plainText = string.Empty;

            // Act
            var encrypted = TokenProtection.Protect(plainText);

            // Assert
            Assert.Equal(string.Empty, encrypted);
        }

        [Fact]
        public void Protect_NonEmptyString_ReturnsEncrypted()
        {
            // Arrange
            var plainText = "MySecretToken123";

            // Act
            var encrypted = TokenProtection.Protect(plainText);

            // Assert
            Assert.NotEqual(plainText, encrypted);
            Assert.StartsWith("ENC:", encrypted);
        }

        [Fact]
        public void Unprotect_ProtectedString_ReturnsOriginal()
        {
            // Arrange
            var original = "MySecretToken123";
            var encrypted = TokenProtection.Protect(original);

            // Act
            var decrypted = TokenProtection.Unprotect(encrypted);

            // Assert
            Assert.Equal(original, decrypted);
        }

        [Fact]
        public void Unprotect_UnprotectedString_ReturnsAsIs()
        {
            // Arrange
            var plainText = "NotEncrypted";

            // Act
            var result = TokenProtection.Unprotect(plainText);

            // Assert
            Assert.Equal(plainText, result);
        }

        [Fact]
        public void IsProtected_EncryptedString_ReturnsTrue()
        {
            // Arrange
            var encrypted = TokenProtection.Protect("test");

            // Act
            var isProtected = TokenProtection.IsProtected(encrypted);

            // Assert
            Assert.True(isProtected);
        }

        [Fact]
        public void IsProtected_PlainString_ReturnsFalse()
        {
            // Arrange
            var plainText = "NotEncrypted";

            // Act
            var isProtected = TokenProtection.IsProtected(plainText);

            // Assert
            Assert.False(isProtected);
        }

        [Fact]
        public void MigrateToProtected_PlainText_ReturnsEncrypted()
        {
            // Arrange
            var plainText = "OldToken";

            // Act
            var migrated = TokenProtection.MigrateToProtected(plainText);

            // Assert
            Assert.NotEqual(plainText, migrated);
            Assert.True(TokenProtection.IsProtected(migrated));
        }

        [Fact]
        public void MigrateToProtected_AlreadyProtected_ReturnsUnchanged()
        {
            // Arrange
            var protectedToken = TokenProtection.Protect("Token");

            // Act
            var migrated = TokenProtection.MigrateToProtected(protectedToken);

            // Assert
            Assert.Equal(protectedToken, migrated);
        }

        [Fact]
        public void Protect_AlreadyProtected_ReturnsUnchanged()
        {
            // Arrange
            var protectedToken = TokenProtection.Protect("Token");

            // Act
            var reProtected = TokenProtection.Protect(protectedToken);

            // Assert
            Assert.Equal(protectedToken, reProtected);
        }

        [Theory]
        [InlineData("oauth:abc123")]
        [InlineData("password123!@#")]
        [InlineData("very_long_token_with_special_chars_!@#$%^&*()")]
        public void ProtectUnprotect_RoundTrip_PreservesOriginal(string original)
        {
            // Act
            var encrypted = TokenProtection.Protect(original);
            var decrypted = TokenProtection.Unprotect(encrypted);

            // Assert
            Assert.Equal(original, decrypted);
        }
    }
}
