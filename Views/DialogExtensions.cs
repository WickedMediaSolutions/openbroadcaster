using Avalonia.Controls;

namespace OpenBroadcaster.Views
{
    public static class DialogExtensions
    {
        public static bool? ShowDialog(this Window window)
        {
            if (UiServices.MainWindow == null)
            {
                window.Show();
                return null;
            }

            return window.ShowDialog<bool?>(UiServices.MainWindow).GetAwaiter().GetResult();
        }
    }
}
