using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OpenBroadcaster.Avalonia.ViewModels;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private ListBox? _chatList;
        private INotifyCollectionChanged? _chatMessages;

        public MainWindow()
        {
            InitializeComponent();
            _chatList = this.FindControl<ListBox>("ChatList");
            DataContextChanged += OnDataContextChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnExitClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_chatMessages != null)
            {
                _chatMessages.CollectionChanged -= OnChatMessagesChanged;
                _chatMessages = null;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                _chatMessages = vm.ChatMessages;
                _chatMessages.CollectionChanged += OnChatMessagesChanged;
            }
        }

        private void OnChatMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_chatList == null || e.NewItems == null || e.NewItems.Count == 0)
            {
                return;
            }

            var last = e.NewItems[e.NewItems.Count - 1];
            Dispatcher.UIThread.Post(() => _chatList.ScrollIntoView(last));
        }

        private void OnAssignFile(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is OpenBroadcaster.Core.Models.CartPad pad && DataContext is MainWindowViewModel vm)
            {
                vm.AssignPadCommand?.Execute(pad);
            }
        }

        private void OnClearPad(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is OpenBroadcaster.Core.Models.CartPad pad && DataContext is MainWindowViewModel vm)
            {
                vm.ClearPadCommand?.Execute(pad);
            }
        }
    }
}
