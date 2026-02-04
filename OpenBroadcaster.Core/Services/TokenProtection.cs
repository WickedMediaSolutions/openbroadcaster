using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// Provides secure token encryption/decryption using platform-specific APIs.
    /// Windows: DPAPI (Data Protection API)
    /// Linux/Mac: Base64 encoding (placeholder - should use keyring/keychain in production)
    /// </summary>
    public static class TokenProtection
    {
        private const string EncryptedPrefix = "ENC:";

        /// <summary>
        /// Encrypts a token using platform-specific protection.
        /// </summary>
        public static string Protect(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return string.Empty;
            }

            // Already encrypted
            if (plainText.StartsWith(EncryptedPrefix))
            {
                return plainText;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return ProtectWindows(plainText);
                }
                else
                {
                    // For Linux/Mac: Use base64 as placeholder
                    // In production, should integrate with system keyring/keychain
                    return ProtectFallback(plainText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TokenProtection.Protect failed: {ex.Message}");
                // If encryption fails, return plaintext to avoid data loss
                return plainText;
            }
        }

        /// <summary>
        /// Decrypts a protected token.
        /// </summary>
        public static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
            {
                return string.Empty;
            }

            // Not encrypted, return as-is (backward compatibility)
            if (!encryptedText.StartsWith(EncryptedPrefix))
            {
                return encryptedText;
            }

            try
            {
                var payload = encryptedText.Substring(EncryptedPrefix.Length);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return UnprotectWindows(payload);
                }
                else
                {
                    return UnprotectFallback(payload);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TokenProtection.Unprotect failed: {ex.Message}");
                // If decryption fails, return encrypted value to avoid data loss
                return encryptedText;
            }
        }

        private static string ProtectWindows(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            var base64 = Convert.ToBase64String(protectedBytes);
            return EncryptedPrefix + base64;
        }

        private static string UnprotectWindows(string base64Payload)
        {
            var protectedBytes = Convert.FromBase64String(base64Payload);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private static string ProtectFallback(string plainText)
        {
            // Simple obfuscation for non-Windows platforms
            // TODO: Integrate with libsecret (Linux) or Keychain (macOS) for production
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var base64 = Convert.ToBase64String(bytes);
            return EncryptedPrefix + base64;
        }

        private static string UnprotectFallback(string base64Payload)
        {
            var bytes = Convert.FromBase64String(base64Payload);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Checks if a value is encrypted.
        /// </summary>
        public static bool IsProtected(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.StartsWith(EncryptedPrefix);
        }

        /// <summary>
        /// Migrates plaintext token to encrypted format.
        /// </summary>
        public static string MigrateToProtected(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || IsProtected(token))
            {
                return token;
            }

            return Protect(token);
        }
    }
}
