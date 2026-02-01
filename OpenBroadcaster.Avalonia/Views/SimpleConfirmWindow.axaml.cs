using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class SimpleConfirmWindow : Window
    {
        public SimpleConfirmWindow()
        {
            InitializeComponent();
        }

        public SimpleConfirmWindow(string title, string message, string detail = "") : this()
        {
            this.Title = title;
            PromptBlock.Text = message;
            DetailBlock.Text = detail;
        }

        private void YesClicked(object? sender, RoutedEventArgs e)
        {
            this.Close(true);
        }

        private void NoClicked(object? sender, RoutedEventArgs e)
        {
            this.Close(false);
        }
    }
}