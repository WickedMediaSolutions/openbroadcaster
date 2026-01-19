using System;
using System.Collections.ObjectModel;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class AppSettingsMigrator
    {
        public void Migrate(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Automation ??= new AutomationSettings();
            settings.Automation.ClockwheelSlots ??= new ObservableCollection<ClockwheelSlotSettings>();
            if (settings.Automation.TargetQueueDepth <= 0)
            {
                settings.Automation.TargetQueueDepth = 5;
            }

            settings.Requests ??= new RequestSettings();
            if (settings.Requests.MaxPendingRequests <= 0)
            {
                settings.Requests.MaxPendingRequests = 25;
            }

            if (settings.Requests.MaxRequestsPerUser <= 0)
            {
                settings.Requests.MaxRequestsPerUser = 3;
            }

            settings.Overlay ??= new OverlaySettings();
            settings.Queue ??= new QueueSettings();
            if (settings.Queue.MaxHistoryItems <= 0)
            {
                settings.Queue.MaxHistoryItems = 5;
            }

            var version = NormalizeVersion(settings.Version);
            var targetVersion = NormalizeVersion(AppSettings.CurrentVersion);
            if (version < targetVersion)
            {
                settings.Version = AppSettings.CurrentVersion;
            }
        }

        private static decimal NormalizeVersion(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return 0m;
            }

            if (decimal.TryParse(input, out var numeric))
            {
                return numeric;
            }

            if (Version.TryParse(input, out var version))
            {
                return Convert.ToDecimal(version.Major) + Convert.ToDecimal(version.Minor) / 10m;
            }

            return 0m;
        }
    }
}
