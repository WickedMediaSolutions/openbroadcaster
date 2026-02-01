using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class SimpleMessageWindow : Window
    {
        public SimpleMessageWindow()
        {
            InitializeComponent();
        }

        public SimpleMessageWindow(string title, string message) : this()
        {
            this.Title = title;
            TitleBlock.Text = title;
            MessageBlock.Text = message;
        }

        private void CloseClicked(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}