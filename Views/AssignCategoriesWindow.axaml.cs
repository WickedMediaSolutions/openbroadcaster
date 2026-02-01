using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OpenBroadcaster.Views
{
    public sealed partial class AssignCategoriesWindow : Window
    {
        public AssignCategoriesWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnSave(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
