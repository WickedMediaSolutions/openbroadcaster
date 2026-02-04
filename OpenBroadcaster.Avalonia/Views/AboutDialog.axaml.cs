using System.Diagnostics;
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
        }

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
    }
}
