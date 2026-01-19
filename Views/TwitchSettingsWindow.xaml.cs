using System.Windows;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public partial class TwitchSettingsWindow : Window
    {
        public TwitchSettingsWindow()
        {
            InitializeComponent();
        }

        private void PasswordBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.PasswordBox passwordBox && DataContext is TwitchSettingsViewModel vm)
            {
                passwordBox.Password = vm.OAuthToken;
                passwordBox.PasswordChanged += (_, __) => vm.OAuthToken = passwordBox.Password;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TwitchSettingsViewModel vm)
            {
                if (!vm.IsValid)
                {
                    System.Windows.MessageBox.Show(this, "Username, OAuth token, and channel are required.", "Twitch Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}
