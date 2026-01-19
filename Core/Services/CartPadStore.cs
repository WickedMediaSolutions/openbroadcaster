using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class CartPadStore
    {
        private readonly string _legacyFilePath;
        private readonly JsonSerializerOptions _legacyOptions = new() { WriteIndented = true };
        private readonly AppSettingsStore _settingsStore;

        public CartPadStore(string? legacyFilePath = null, AppSettingsStore? settingsStore = null)
        {
            _legacyFilePath = legacyFilePath ?? Path.Combine(AppContext.BaseDirectory, "cartwall.json");
            _settingsStore = settingsStore ?? new AppSettingsStore();
        }

        public IReadOnlyList<CartPadSnapshot> Load()
        {
            var settings = _settingsStore.Load();
            if (settings.CartWall?.Pads != null && settings.CartWall.Pads.Count > 0)
            {
                return settings.CartWall.Pads.Select(MapFromSettings).ToList();
            }

            var legacy = LoadLegacySnapshot();
            if (legacy.Count > 0)
            {
                settings.CartWall ??= new CartWallSettings();
                settings.CartWall.Pads = legacy.Select(MapToSettings).ToList();
                _settingsStore.Save(settings);
            }

            return legacy;
        }

        public void Save(IEnumerable<CartPad> pads)
        {
            if (pads == null)
            {
                return;
            }

            var settings = _settingsStore.Load();
            settings.CartWall ??= new CartWallSettings();
            settings.CartWall.Pads ??= new List<CartPadSettings>();
            settings.CartWall.Pads.Clear();

            foreach (var pad in pads)
            {
                settings.CartWall.Pads.Add(new CartPadSettings
                {
                    Id = pad.Id,
                    Label = pad.Label,
                    FilePath = pad.FilePath,
                    ColorHex = pad.ColorHex,
                    Hotkey = pad.Hotkey,
                    LoopEnabled = pad.LoopEnabled
                });
            }

            _settingsStore.Save(settings);
        }

        private IReadOnlyList<CartPadSnapshot> LoadLegacySnapshot()
        {
            if (!File.Exists(_legacyFilePath))
            {
                return Array.Empty<CartPadSnapshot>();
            }

            try
            {
                var json = File.ReadAllText(_legacyFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return Array.Empty<CartPadSnapshot>();
                }

                var pads = JsonSerializer.Deserialize<List<CartPadSnapshot>>(json, _legacyOptions);
                return pads ?? new List<CartPadSnapshot>();
            }
            catch
            {
                return Array.Empty<CartPadSnapshot>();
            }
        }

        private static CartPadSnapshot MapFromSettings(CartPadSettings settings)
        {
            return new CartPadSnapshot
            {
                Id = settings.Id,
                Label = settings.Label,
                FilePath = settings.FilePath,
                ColorHex = settings.ColorHex,
                Hotkey = settings.Hotkey,
                LoopEnabled = settings.LoopEnabled
            };
        }

        private static CartPadSettings MapToSettings(CartPadSnapshot snapshot)
        {
            return new CartPadSettings
            {
                Id = snapshot.Id,
                Label = snapshot.Label,
                FilePath = snapshot.FilePath,
                ColorHex = snapshot.ColorHex,
                Hotkey = snapshot.Hotkey,
                LoopEnabled = snapshot.LoopEnabled
            };
        }

        public sealed class CartPadSnapshot
        {
            public int Id { get; set; }
            public string Label { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string ColorHex { get; set; } = "#FF151C29";
            public string Hotkey { get; set; } = string.Empty;
            public bool LoopEnabled { get; set; }
        }
    }
}
