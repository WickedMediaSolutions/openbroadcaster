using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            AppVersion = BuildAppVersion();
            DataContext = this;
        }

        public string AppVersion { get; }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnTwitchClick(object? sender, PointerPressedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://twitch.tv/bluntforcejosh",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OnGitHubClick(object? sender, PointerPressedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/WickedMediaSolutions/openbroadcaster",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static string BuildAppVersion()
        {
            var assembly = typeof(AboutDialog).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = string.IsNullOrWhiteSpace(info) ? assembly.GetName().Version?.ToString() : info;
            return string.IsNullOrWhiteSpace(version) ? "Version unknown" : $"Version {version}";
        }
    }
}
