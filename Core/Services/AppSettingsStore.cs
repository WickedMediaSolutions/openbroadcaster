using System;
using System.IO;
using System.Text.Json;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class AppSettingsStore
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
        private readonly AppSettingsMigrator _migrator = new();

        public AppSettingsStore(string? filePath = null)
        {
            _filePath = filePath ?? GetDefaultPath();
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                return CreateDefault();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateDefault();
                }

                var settings = JsonSerializer.Deserialize<AppSettings>(json, _options) ?? CreateDefault();
                settings.ApplyDefaults();
                _migrator.Migrate(settings);
                
                // Decrypt sensitive tokens after loading
                DecryptSensitiveData(settings);
                
                return settings;
            }
            catch (Exception)
            {
                return CreateDefault();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.ApplyDefaults();

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Clone settings to avoid modifying the original
            var clonedSettings = CloneSettings(settings);
            
            // Encrypt sensitive tokens before saving
            EncryptSensitiveData(clonedSettings);

            var payload = JsonSerializer.Serialize(clonedSettings, _options);
            File.WriteAllText(_filePath, payload);
        }

        private static void EncryptSensitiveData(AppSettings settings)
        {
            // Encrypt Twitch OAuth token
            if (!string.IsNullOrWhiteSpace(settings.Twitch.OAuthToken))
            {
                settings.Twitch.OAuthToken = TokenProtection.Protect(settings.Twitch.OAuthToken);
            }

            // Encrypt Overlay API password
            if (!string.IsNullOrWhiteSpace(settings.Overlay.ApiPassword))
            {
                settings.Overlay.ApiPassword = TokenProtection.Protect(settings.Overlay.ApiPassword);
            }
        }

        private static void DecryptSensitiveData(AppSettings settings)
        {
            // Decrypt Twitch OAuth token
            if (!string.IsNullOrWhiteSpace(settings.Twitch.OAuthToken))
            {
                settings.Twitch.OAuthToken = TokenProtection.Unprotect(settings.Twitch.OAuthToken);
            }

            // Decrypt Overlay API password
            if (!string.IsNullOrWhiteSpace(settings.Overlay.ApiPassword))
            {
                settings.Overlay.ApiPassword = TokenProtection.Unprotect(settings.Overlay.ApiPassword);
            }
        }

        private static AppSettings CloneSettings(AppSettings original)
        {
            // Deep clone to avoid modifying original during encryption
            var json = JsonSerializer.Serialize(original);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }

        private static AppSettings CreateDefault()
        {
            var settings = new AppSettings();
            settings.ApplyDefaults();
            return settings;
        }

        private static string GetDefaultPath()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(baseDir, "OpenBroadcaster");
            return Path.Combine(appDir, "settings.json");
        }
    }
}
