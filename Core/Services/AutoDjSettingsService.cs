using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster.Core.Services
{
    /// <summary>
    /// Manages loading and saving of AutoDJ settings, including rotations and schedule rules.
    /// </summary>
    public class AutoDjSettingsService
    {
        private const string RotationsFileName = "autodj_rotations.json";
        private const string RulesFileName = "autodj_rules.json";
        private const string SettingsFileName = "autodj_settings.json";
        private const string ScheduleFileName = "autodj_schedule.json";

        private readonly string _settingsPath;
        private readonly ILogger<AutoDjSettingsService> _logger;

        public virtual List<SimpleRotation> Rotations { get; set; } = new();
        public virtual List<SimpleScheduleRule> Rules { get; set; } = new();
        public virtual List<SimpleSchedulerEntry> Schedule { get; set; } = new();
        public virtual Guid DefaultRotationId { get; set; } = Guid.Empty;
        public virtual string DefaultRotationName { get; set; } = string.Empty;

        public AutoDjSettingsService(bool loadFromDisk = true)
        {
            _logger = AppLogger.CreateLogger<AutoDjSettingsService>();
            _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenBroadcaster");
            if (!Directory.Exists(_settingsPath))
            {
                Directory.CreateDirectory(_settingsPath);
            }

            if (loadFromDisk)
            {
                LoadAll();
                EnsureDefaultRotation();
                SaveAll();
            }
        }

        /// <summary>
        /// Loads all AutoDJ settings from their respective JSON files.
        /// </summary>
        public void LoadAll()
        {
            Rotations = Load<List<SimpleRotation>>(RotationsFileName) ?? new List<SimpleRotation>();
            Rules = Load<List<SimpleScheduleRule>>(RulesFileName) ?? new List<SimpleScheduleRule>();
            Schedule = Load<List<SimpleSchedulerEntry>>(ScheduleFileName) ?? new List<SimpleSchedulerEntry>();
            var settings = Load<AutoDjGeneralSettings>(SettingsFileName);
            DefaultRotationId = settings?.DefaultRotationId ?? Guid.Empty;
            DefaultRotationName = settings?.DefaultRotationName ?? string.Empty;
        }

        /// <summary>
        /// Saves all AutoDJ settings to their respective JSON files.
        /// </summary>
        public void SaveAll()
        {
            Save(Rotations, RotationsFileName);
            Save(Rules, RulesFileName);
            Save(Schedule, ScheduleFileName);
            Save(new AutoDjGeneralSettings { DefaultRotationId = this.DefaultRotationId, DefaultRotationName = this.DefaultRotationName }, SettingsFileName);
        }

        private void EnsureDefaultRotation()
        {
            if (DefaultRotationId == Guid.Empty && Rotations.Count > 0)
            {
                var first = Rotations.First();
                DefaultRotationId = first.Id;
                DefaultRotationName = first.Name;
            }
            
            // CRITICAL: Ensure at least one rotation is marked as Active
            if (Rotations.Count > 0 && !Rotations.Any(r => r.IsActive))
            {
                // Mark first enabled rotation as active, or just first rotation
                var toActivate = Rotations.FirstOrDefault(r => r.Enabled) ?? Rotations.First();
                toActivate.IsActive = true;
                _logger.LogInformation("Auto-marked rotation '{RotationName}' as active", toActivate.Name);
            }
        }

        private T? Load<T>(string fileName) where T : class
        {
            var filePath = Path.Combine(_settingsPath, fileName);
            if (!File.Exists(filePath))
            {
                return null;
            }
            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load {FileName}", fileName);
                return null;
            }
        }

        private void Save<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_settingsPath, fileName);
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save {FileName}", fileName);
            }
        }

        /// <summary>
        /// Helper class for storing general AutoDJ settings.
        /// </summary>
        private class AutoDjGeneralSettings
        {
            public Guid DefaultRotationId { get; set; }
            public string DefaultRotationName { get; set; } = string.Empty;
        }
    }
}
