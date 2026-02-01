using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void AssignCartTrack_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem)
                return;

            // The MenuItem's DataContext should be the CartPad
            CartPad? pad = menuItem.DataContext as CartPad;

            if (pad != null && DataContext is MainViewModel vm)
            {
                await vm.AssignCartPadFromPickerPublicAsync(pad);
            }
        }
    }
}
