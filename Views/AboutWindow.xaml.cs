using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace OpenBroadcaster.Views
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            AppVersion = BuildAppVersion();
            DataContext = this;
        }

        public string AppVersion { get; }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private static string BuildAppVersion()
        {
            var assembly = typeof(AboutWindow).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = string.IsNullOrWhiteSpace(info) ? assembly.GetName().Version?.ToString() : info;
            return string.IsNullOrWhiteSpace(version) ? "Version unknown" : $"Version {version}";
        }
    }
}
