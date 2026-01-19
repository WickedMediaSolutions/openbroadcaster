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

            var payload = JsonSerializer.Serialize(settings, _options);
            File.WriteAllText(_filePath, payload);
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
