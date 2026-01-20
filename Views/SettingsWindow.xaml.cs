using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => { SyncOAuthPassword(); SyncApiPassword(); };
        }

        private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

        private void OnBindingSourceUpdated(object? sender, DataTransferEventArgs e)
        {
            ViewModel?.NotifySettingsModified();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.Apply();
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.Cancel();
            DialogResult = false;
            Close();
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.Apply();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            SyncOAuthPassword();
            SyncApiPassword();
        }

        private void OnOAuthPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box || ViewModel?.Settings?.Twitch == null)
            {
                return;
            }

            var current = ViewModel.Settings.Twitch.OAuthToken ?? string.Empty;
            if (!string.Equals(current, box.Password, StringComparison.Ordinal))
            {
                ViewModel.Settings.Twitch.OAuthToken = box.Password;
                ViewModel.NotifySettingsModified();
            }
        }

        private void SyncOAuthPassword()
        {
            if (OAuthTokenBox == null || ViewModel?.Settings?.Twitch == null)
            {
                return;
            }

            var token = ViewModel.Settings.Twitch.OAuthToken ?? string.Empty;
            if (!string.Equals(OAuthTokenBox.Password, token, StringComparison.Ordinal))
            {
                OAuthTokenBox.Password = token;
            }
        }

        private void OnApiPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box || ViewModel?.Settings?.Overlay == null)
            {
                return;
            }

            var current = ViewModel.Settings.Overlay.ApiPassword ?? string.Empty;
            if (!string.Equals(current, box.Password, StringComparison.Ordinal))
            {
                ViewModel.Settings.Overlay.ApiPassword = box.Password;
                ViewModel.NotifySettingsModified();
            }
        }

        private void SyncApiPassword()
        {
            if (ApiPasswordBox == null || ViewModel?.Settings?.Overlay == null)
            {
                return;
            }

            var password = ViewModel.Settings.Overlay.ApiPassword ?? string.Empty;
            if (!string.Equals(ApiPasswordBox.Password, password, StringComparison.Ordinal))
            {
                ApiPasswordBox.Password = password;
            }
        }

        private void OnBrowseOverlayArtworkClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.Settings?.Overlay == null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.svg;*.gif|All Files|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                if (!string.Equals(ViewModel.Settings.Overlay.ArtworkFallbackFilePath, dialog.FileName, StringComparison.Ordinal))
                {
                    ViewModel.Settings.Overlay.ArtworkFallbackFilePath = dialog.FileName;
                    ViewModel.NotifySettingsModified();
                }
            }
        }

        private void EncoderAdminPassword_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box || box.Tag is not EncoderProfile profile)
            {
                return;
            }

            var current = profile.AdminPassword ?? string.Empty;
            if (!string.Equals(box.Password, current, StringComparison.Ordinal))
            {
                box.Password = current;
            }
        }

        private void EncoderAdminPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box || box.Tag is not EncoderProfile profile)
            {
                return;
            }

            var incoming = box.Password ?? string.Empty;
            if (!string.Equals(profile.AdminPassword ?? string.Empty, incoming, StringComparison.Ordinal))
            {
                profile.AdminPassword = incoming;
                ViewModel?.NotifySettingsModified();
            }
        }

        private void OnSimpleRotationDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.EditSimpleRotationCommand?.CanExecute(null) == true)
            {
                ViewModel.EditSimpleRotationCommand.Execute(null);
            }
        }

        private void OnSimpleScheduleDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.EditSimpleScheduleEntryCommand?.CanExecute(null) == true)
            {
                ViewModel.EditSimpleScheduleEntryCommand.Execute(null);
            }
        }
    }
}
