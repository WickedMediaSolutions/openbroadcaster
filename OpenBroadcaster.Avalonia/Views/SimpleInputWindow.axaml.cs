using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class SimpleInputWindow : Window
    {
        public SimpleInputWindow()
        {
            InitializeComponent();
        }

        public SimpleInputWindow(string prompt, string defaultValue = "") : this()
        {
            PromptBlock.Text = prompt;
            InputBox.Text = defaultValue;
        }

        public string Result => InputBox.Text ?? string.Empty;

        private void OkClicked(object? sender, RoutedEventArgs e)
        {
            this.Close(true);
        }

        private void CancelClicked(object? sender, RoutedEventArgs e)
        {
            this.Close(false);
        }
    }
}