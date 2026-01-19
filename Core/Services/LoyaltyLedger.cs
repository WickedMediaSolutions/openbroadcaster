using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OpenBroadcaster.Core.Services
{
    public sealed class LoyaltyLedger
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
        private readonly Dictionary<string, LoyaltyEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        public LoyaltyLedger(string? filePath = null)
        {
            _filePath = filePath ?? ResolveDefaultLedgerPath();
            if (filePath == null)
            {
                TryMigrateLegacyLedger();
            }

            Load();
        }

        public int GetPoints(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return 0;
            }

            lock (_sync)
            {
                return _entries.TryGetValue(userName, out var entry) ? entry.Points : 0;
            }
        }

        public int AddPoints(string userName, int delta)
        {
            if (string.IsNullOrWhiteSpace(userName) || delta == 0)
            {
                return GetPoints(userName);
            }

            lock (_sync)
            {
                if (!_entries.TryGetValue(userName, out var entry))
                {
                    entry = new LoyaltyEntry { UserName = userName, Points = 0, LastUpdatedUtc = DateTime.UtcNow };
                }

                entry.Points = Math.Max(0, entry.Points + delta);
                entry.LastUpdatedUtc = DateTime.UtcNow;
                _entries[userName] = entry;
                Persist();
                return entry.Points;
            }
        }

        public bool TryDebit(string userName, int cost, out int remaining)
        {
            remaining = 0;
            if (string.IsNullOrWhiteSpace(userName) || cost <= 0)
            {
                return false;
            }

            lock (_sync)
            {
                if (!_entries.TryGetValue(userName, out var entry))
                {
                    return false;
                }

                if (entry.Points < cost)
                {
                    remaining = entry.Points;
                    return false;
                }

                entry.Points -= cost;
                entry.LastUpdatedUtc = DateTime.UtcNow;
                _entries[userName] = entry;
                Persist();
                remaining = entry.Points;
                return true;
            }
        }

        public IReadOnlyDictionary<string, LoyaltyEntry> Snapshot()
        {
            lock (_sync)
            {
                return new Dictionary<string, LoyaltyEntry>(_entries, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var restored = JsonSerializer.Deserialize<Dictionary<string, LoyaltyEntry>>(json, _options);
                if (restored == null)
                {
                    return;
                }

                foreach (var kvp in restored)
                {
                    _entries[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception)
            {
                // Ignore corrupt ledgers; a fresh one will be created.
            }
        }

        private void Persist()
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.Serialize(_entries, _options);
            File.WriteAllText(_filePath, payload);
        }

        private static string ResolveDefaultLedgerPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var root = Path.Combine(appData, "OpenBroadcaster");
            return Path.Combine(root, "loyalty.json");
        }

        private void TryMigrateLegacyLedger()
        {
            var legacyPath = Path.Combine(AppContext.BaseDirectory, "loyalty.json");
            if (File.Exists(_filePath) || !File.Exists(legacyPath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(legacyPath, _filePath, overwrite: false);
            }
            catch
            {
                // Ignore migration errors; a fresh ledger will be created when needed.
            }
        }
    }

    public sealed class LoyaltyEntry
    {
        public string UserName { get; set; } = string.Empty;
        public int Points { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
