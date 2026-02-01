using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public sealed partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            ViewModel?.Apply();
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            ViewModel?.Cancel();
            Close(false);
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            ViewModel?.Apply();
        }

        private async void OnBrowseOverlayArtworkClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel?.Settings?.Overlay == null)
            {
                return;
            }

            var files = await UiServices.OpenFilesAsync("Select artwork fallback", "png;jpg;jpeg;svg;gif", false);
            var selected = files?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            if (!string.Equals(ViewModel.Settings.Overlay.ArtworkFallbackFilePath, selected, StringComparison.Ordinal))
            {
                ViewModel.Settings.Overlay.ArtworkFallbackFilePath = selected;
                ViewModel.NotifySettingsModified();
            }
        }

        private void OnSimpleRotationDoubleClick(object? sender, TappedEventArgs e)
        {
            if (ViewModel?.EditSimpleRotationCommand?.CanExecute(null) == true)
            {
                ViewModel.EditSimpleRotationCommand.Execute(null);
            }
        }

        private void OnSimpleScheduleDoubleClick(object? sender, TappedEventArgs e)
        {
            if (ViewModel?.EditSimpleScheduleEntryCommand?.CanExecute(null) == true)
            {
                ViewModel.EditSimpleScheduleEntryCommand.Execute(null);
            }
        }
    }
}
