using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using OpenBroadcaster.Core.Models;
using System.Linq;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class AssignCategoriesWindow : Window
    {
        private OpenBroadcaster.Avalonia.ViewModels.AssignCategoriesViewModel? _vm;

        public AssignCategoriesWindow()
        {
            InitializeComponent();
            this.Opened += AssignCategoriesWindow_Opened;
        }

        private void AssignCategoriesWindow_Opened(object? sender, System.EventArgs e)
        {
            _vm = this.DataContext as OpenBroadcaster.Avalonia.ViewModels.AssignCategoriesViewModel;
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_vm == null) return;
            CategoriesPanel.Children.Clear();
            foreach (var c in _vm.Categories)
            {
                // Create content: optional star + name
                var content = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (c.IsToh)
                {
                    var star = new TextBlock() { Text = "★", Foreground = Brushes.DarkOrange, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                    content.Children.Add(star);
                }
                var nameBlock = new TextBlock() { Text = c.Name, VerticalAlignment = VerticalAlignment.Center };
                if (c.IsToh)
                {
                    nameBlock.Foreground = Brushes.Gray;
                    nameBlock.Opacity = 0.85;
                }
                content.Children.Add(nameBlock);

                var cb = new CheckBox() { Content = content, IsChecked = c.IsChecked };
                // Make permanent TOH categories read-only in this UI
                cb.IsEnabled = !c.IsToh;
                if (c.IsToh)
                {
                    ToolTip.SetTip(cb, "Top-of-Hour (TOH) — permanent category; assignment is managed elsewhere.");
                }
                cb.IsCheckedChanged += (_, __) =>
                {
                    c.IsChecked = cb.IsChecked == true;
                };
                CategoriesPanel.Children.Add(cb);
            }
        }

        private void OkClicked(object? sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            try
            {
                _vm.Apply();
            }
            catch { }
            this.Close(true);
        }

        private void CancelClicked(object? sender, RoutedEventArgs e)
        {
            this.Close(false);
        }
    }
}