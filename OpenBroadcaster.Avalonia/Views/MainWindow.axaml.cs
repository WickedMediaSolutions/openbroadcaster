using System;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenBroadcaster.Avalonia.ViewModels;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        // Constants - using PascalCase per C# conventions
        private const double DragThresholdPixels = 5.0; // Minimum movement in pixels to initiate drag

        // Fields
        private ListBox? _chatList;
        private ListBox? _libraryList;
        private ListBox? _queueList;
        private Border? _deckADropTarget;
        private Border? _deckBDropTarget;
        private INotifyCollectionChanged? _chatMessages;
        private Point _dragStartPoint;
        private bool _isDragStarted;

        public MainWindow()
        {
            InitializeComponent();
            _chatList = this.FindControl<ListBox>("ChatList");
            _libraryList = this.FindControl<ListBox>("LibraryList");
            _queueList = this.FindControl<ListBox>("QueueList");
            _deckADropTarget = this.FindControl<Border>("DeckADropTarget");
            _deckBDropTarget = this.FindControl<Border>("DeckBDropTarget");
            DataContextChanged += OnDataContextChanged;
            Closed += OnClosed;
            
            SetupDragDrop();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            // Remove event subscriptions to prevent memory leaks
            if (_libraryList != null)
            {
                _libraryList.PointerPressed -= OnLibraryPointerPressed;
                _libraryList.PointerMoved -= OnLibraryPointerMoved;
            }
            if (_queueList != null)
            {
                _queueList.PointerPressed -= OnQueuePointerPressed;
                _queueList.PointerMoved -= OnQueuePointerMoved;
            }
            
            // Dispose ViewModel when window closes
            (DataContext as IDisposable)?.Dispose();
        }

        private void SetupDragDrop()
        {
            // Library drag source
            if (_libraryList != null)
            {
                _libraryList.PointerPressed += OnLibraryPointerPressed;
                _libraryList.PointerMoved += OnLibraryPointerMoved;
            }

            // Queue drag source and drop target
            if (_queueList != null)
            {
                _queueList.PointerPressed += OnQueuePointerPressed;
                _queueList.PointerMoved += OnQueuePointerMoved;
                _queueList.AddHandler(DragDrop.DropEvent, OnQueueDrop);
                _queueList.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            }

            // Deck drop targets
            if (_deckADropTarget != null)
            {
                _deckADropTarget.AddHandler(DragDrop.DropEvent, OnDeckADrop);
                _deckADropTarget.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            }

            if (_deckBDropTarget != null)
            {
                _deckBDropTarget.AddHandler(DragDrop.DropEvent, OnDeckBDrop);
                _deckBDropTarget.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            }
        }

        private void OnLibraryPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragStartPoint = e.GetPosition(_libraryList);
            _isDragStarted = false;
        }

        private async void OnLibraryPointerMoved(object? sender, PointerEventArgs e)
        {
            try
            {
                if (_isDragStarted || _libraryList == null) return;
                
                var currentPoint = e.GetCurrentPoint(_libraryList);
                if (!currentPoint.Properties.IsLeftButtonPressed) return;
                
                var currentPosition = e.GetPosition(_libraryList);
                var diff = currentPosition - _dragStartPoint;
                
                // Check if we've moved enough to start a drag
                if (Math.Abs(diff.X) < DragThresholdPixels && Math.Abs(diff.Y) < DragThresholdPixels) return;
                
                if (_libraryList?.SelectedItem is not LibraryItemViewModel item) return;
                
                _isDragStarted = true;
                
                var data = new DataObject();
                data.Set("LibraryItem", item);
                
                try
                {
                    await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Move);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during drag-drop in OnLibraryPointerMoved: {ex.Message}");
                }
                finally
                {
                    _isDragStarted = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnLibraryPointerMoved: {ex.Message}");
                _isDragStarted = false;
            }
        }

        private void OnQueuePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragStartPoint = e.GetPosition(_queueList);
            _isDragStarted = false;
        }

        private async void OnQueuePointerMoved(object? sender, PointerEventArgs e)
        {
            try
            {
                if (_isDragStarted) return;
                
                var currentPoint = e.GetCurrentPoint(_queueList);
                if (!currentPoint.Properties.IsLeftButtonPressed) return;
                
                var currentPosition = e.GetPosition(_queueList);
                var diff = currentPosition - _dragStartPoint;
                
                // Check if we've moved enough to start a drag
                if (Math.Abs(diff.X) < DragThresholdPixels && Math.Abs(diff.Y) < DragThresholdPixels) return;
                
                if (_queueList?.SelectedItem is not QueueItemViewModel item) return;
                
                _isDragStarted = true;
                
                var data = new DataObject();
                data.Set("QueueItem", item);
                
                try
                {
                    await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during drag-drop in OnQueuePointerMoved: {ex.Message}");
                }
                finally
                {
                    _isDragStarted = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnQueuePointerMoved: {ex.Message}");
                _isDragStarted = false;
            }
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        }

        private void OnQueueDrop(object? sender, DragEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // Library item dropped on queue
            if (e.Data.Get("LibraryItem") is LibraryItemViewModel libraryItem)
            {
                // Find drop position
                var dropIndex = GetDropIndex(e);
                if (dropIndex >= 0)
                {
                    vm.InsertLibraryItemToQueueAt(libraryItem, dropIndex);
                }
                else
                {
                    vm.AddLibraryItemToQueue(libraryItem);
                }
                e.Handled = true;
                return;
            }

            // Queue item dropped on queue (reorder)
            if (e.Data.Get("QueueItem") is QueueItemViewModel queueItem)
            {
                var fromIndex = vm.GetQueueItemIndex(queueItem);
                var toIndex = GetDropIndex(e);
                if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
                {
                    // Adjust index if dropping after the original position
                    if (toIndex > fromIndex) toIndex--;
                    vm.ReorderQueueItem(fromIndex, toIndex);
                }
                e.Handled = true;
            }
        }

        private int GetDropIndex(DragEventArgs e)
        {
            if (_queueList == null) return -1;
            
            var position = e.GetPosition(_queueList);
            var items = _queueList.Items;
            if (items == null) return 0;

            var index = 0;
            foreach (var item in items)
            {
                var container = _queueList.ContainerFromItem(item);
                if (container is Control control)
                {
                    var bounds = control.Bounds;
                    var midY = bounds.Y + bounds.Height / 2;
                    if (position.Y < midY)
                    {
                        return index;
                    }
                }
                index++;
            }
            return index;
        }

        private void OnDeckADrop(object? sender, DragEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            if (e.Data.Get("LibraryItem") is LibraryItemViewModel libraryItem)
            {
                vm.LoadLibraryItemToDeck(libraryItem, DeckIdentifier.A);
                e.Handled = true;
            }
        }

        private void OnDeckBDrop(object? sender, DragEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            if (e.Data.Get("LibraryItem") is LibraryItemViewModel libraryItem)
            {
                vm.LoadLibraryItemToDeck(libraryItem, DeckIdentifier.B);
                e.Handled = true;
            }
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
