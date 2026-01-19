using System;
using System.IO;
using System.Text.Json;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class TwitchSettingsStore
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

        public TwitchSettingsStore(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "twitch.settings.json");
        }

        public TwitchSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                return new TwitchSettings();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<TwitchSettings>(json, _options);
                return settings ?? new TwitchSettings();
            }
            catch (Exception)
            {
                return new TwitchSettings();
            }
        }

        public void Save(TwitchSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, _options);
            File.WriteAllText(_filePath, json);
        }
    }
}
