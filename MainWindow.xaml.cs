using System;
using System.Linq;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.ViewModels;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace OpenBroadcaster
{
    public partial class MainWindow : Window
    {
        private const string LibraryDragFormat = "OpenBroadcaster/LibraryTrackIds";
        private const string QueueDragFormat = "OpenBroadcaster/QueueReorder";
        private Point? _libraryDragStartPoint;
        private Point? _queueDragStartPoint;
        private QueueItemViewModel? _queueDragItem;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            _radioService = new RadioService(ViewModel);
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private readonly RadioService _radioService;

        private void DeckPlay_Click(object sender, RoutedEventArgs e)
        {
            var origin = sender as DependencyObject ?? e.OriginalSource as DependencyObject;
            var border = FindAncestor<Border>(origin ?? this);
            if (TryGetDeckIdentifier(border ?? (DependencyObject)this, out var deckId))
            {
                _radioService.ActiveDeck = deckId;
            }

            _radioService.Play();
        }

        private void DeckStop_Click(object sender, RoutedEventArgs e)
        {
            var origin = sender as DependencyObject ?? e.OriginalSource as DependencyObject;
            var border = FindAncestor<Border>(origin ?? this);
            if (TryGetDeckIdentifier(border ?? (DependencyObject)this, out var deckId))
            {
                _radioService.ActiveDeck = deckId;
            }

            _radioService.Stop();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnClosed(e);
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBarBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the user clicked on one of the caption buttons, let the button handle it
            if (e.OriginalSource is DependencyObject origin)
            {
                var button = FindAncestor<System.Windows.Controls.Button>(origin);
                if (button != null)
                {
                    return;
                }

                // Also allow clicks on the main menu and its items
                var menu = FindAncestor<System.Windows.Controls.Menu>(origin);
                if (menu != null)
                {
                    return;
                }

                var menuItem = FindAncestor<System.Windows.Controls.MenuItem>(origin);
                if (menuItem != null)
                {
                    return;
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximizeRestore();
                }
                else
                {
                    try
                    {
                        DragMove();
                    }
                    catch
                    {
                        // Ignore drag exceptions (e.g. during window state changes)
                    }
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ChatMessages is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged += ChatMessages_CollectionChanged;
            }
        }

        private void ChatMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Always keep Twitch chat scrolled to the newest message
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TwitchChatListBox == null || TwitchChatListBox.Items.Count == 0)
                {
                    return;
                }

                var lastItem = TwitchChatListBox.Items[TwitchChatListBox.Items.Count - 1];
                TwitchChatListBox.ScrollIntoView(lastItem);
            }));
        }

        private void LibraryListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _libraryDragStartPoint = e.GetPosition(null);
        }

        private void LibraryListView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _libraryDragStartPoint == null)
            {
                return;
            }

            var position = e.GetPosition(null);
            if (IsDragThresholdMet(_libraryDragStartPoint.Value, position))
            {
                _libraryDragStartPoint = null;
                StartLibraryDrag();
            }
        }

        private void QueueListBox_DragOver(object sender, DragEventArgs e)
        {
            if (TryGetQueueDragPayload(e.Data, out _))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                HandleLibraryDragOver(e);
                return;
            }

            e.Handled = true;
        }

        private void QueueListBox_Drop(object sender, DragEventArgs e)
        {
            if (TryGetQueueDragPayload(e.Data, out var payload) && payload != null)
            {
                var targetIndex = GetQueueIndexFromPoint(e.GetPosition(QueueListBox));
                if (targetIndex >= 0)
                {
                    ViewModel?.ReorderQueueItem(payload.SourceIndex, targetIndex);
                }
            }
            else if (TryGetLibraryTrackIds(e.Data, out var trackIds) && trackIds.Length > 0)
            {
                ViewModel?.QueueLibraryTracks(trackIds);
            }

            e.Handled = true;
            ResetQueueDragState();
        }

        private void DeckSurface_DragOver(object sender, DragEventArgs e)
        {
            HandleLibraryDragOver(e);
        }

        private void DeckSurface_Drop(object sender, DragEventArgs e)
        {
            if (TryGetDeckIdentifier(sender, out var deckId) && TryGetLibraryTrackIds(e.Data, out var trackIds) && trackIds.Length > 0)
            {
                ViewModel?.LoadDeckFromLibrary(trackIds[0], deckId);
            }

            e.Handled = true;
        }

        private void StartLibraryDrag()
        {
            if (LibraryListView?.SelectedItems == null || LibraryListView.SelectedItems.Count == 0)
            {
                return;
            }

            var trackIds = LibraryListView.SelectedItems
                .OfType<SongLibraryItemViewModel>()
                .Select(item => item.TrackId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            if (trackIds.Length == 0)
            {
                return;
            }

            var data = new DataObject(LibraryDragFormat, trackIds);
            DragDrop.DoDragDrop(LibraryListView, data, DragDropEffects.Copy);
        }

        private void QueueListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _queueDragStartPoint = e.GetPosition(null);
            _queueDragItem = ResolveQueueItem(e.OriginalSource as DependencyObject);
        }

        private void QueueListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (QueueListBox == null)
            {
                return;
            }

            var item = ResolveQueueItem(e.OriginalSource as DependencyObject);
            if (item != null)
            {
                QueueListBox.SelectedItem = item;
            }
        }

        private void QueueListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _queueDragStartPoint == null || _queueDragItem == null)
            {
                return;
            }

            var current = e.GetPosition(null);
            if (IsDragThresholdMet(_queueDragStartPoint.Value, current))
            {
                StartQueueDrag();
                e.Handled = true;
            }
        }

        private void StartQueueDrag()
        {
            if (QueueListBox == null || _queueDragItem == null)
            {
                ResetQueueDragState();
                return;
            }

            var index = QueueListBox.Items.IndexOf(_queueDragItem);
            if (index < 0)
            {
                ResetQueueDragState();
                return;
            }

            var payload = new QueueDragPayload(index);
            var data = new DataObject(QueueDragFormat, payload);
            DragDrop.DoDragDrop(QueueListBox, data, DragDropEffects.Move);
            ResetQueueDragState();
        }

        private static void HandleLibraryDragOver(DragEventArgs e)
        {
            if (TryGetLibraryTrackIds(e.Data, out _))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private static bool TryGetLibraryTrackIds(IDataObject data, out Guid[] trackIds)
        {
            if (data.GetDataPresent(LibraryDragFormat) && data.GetData(LibraryDragFormat) is Guid[] ids)
            {
                trackIds = ids;
                return true;
            }

            trackIds = Array.Empty<Guid>();
            return false;
        }

        private static bool TryGetQueueDragPayload(IDataObject data, out QueueDragPayload? payload)
        {
            if (data.GetDataPresent(QueueDragFormat) && data.GetData(QueueDragFormat) is QueueDragPayload info)
            {
                payload = info;
                return true;
            }

            payload = null;
            return false;
        }

        private int GetQueueIndexFromPoint(Point point)
        {
            if (QueueListBox == null)
            {
                return -1;
            }

            var element = QueueListBox.InputHitTest(point) as DependencyObject;
            if (element == null)
            {
                return QueueListBox.Items.Count - 1;
            }

            var listBoxItem = ItemsControl.ContainerFromElement(QueueListBox, element) as ListBoxItem;
            if (listBoxItem == null)
            {
                return QueueListBox.Items.Count - 1;
            }

            return QueueListBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        }

        private QueueItemViewModel? ResolveQueueItem(DependencyObject? origin)
        {
            if (QueueListBox == null || origin == null)
            {
                return null;
            }

            var container = FindAncestor<ListBoxItem>(origin);
            return container?.DataContext as QueueItemViewModel;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void ResetQueueDragState()
        {
            _queueDragStartPoint = null;
            _queueDragItem = null;
        }

        private static bool TryGetDeckIdentifier(object sender, out DeckIdentifier deckId)
        {
            deckId = DeckIdentifier.A;
            if (sender is FrameworkElement element && element.Tag is string tag && Enum.TryParse(tag, out DeckIdentifier parsed))
            {
                deckId = parsed;
                return true;
            }

            return false;
        }

        private static bool IsDragThresholdMet(Point start, Point current)
        {
            var minX = SystemParameters.MinimumHorizontalDragDistance;
            var minY = SystemParameters.MinimumVerticalDragDistance;
            return Math.Abs(current.X - start.X) >= minX || Math.Abs(current.Y - start.Y) >= minY;
        }

        private sealed class RadioService
        {
            private readonly MainViewModel? _vm;

            public RadioService(MainViewModel? viewModel)
            {
                _vm = viewModel;
                ActiveDeck = DeckIdentifier.A;
            }

            public DeckIdentifier ActiveDeck { get; set; }

            public void Play()
            {
                if (_vm == null)
                {
                    return;
                }

                if (ActiveDeck == DeckIdentifier.A)
                {
                    _vm.DeckA.PlayCommand.Execute(null);
                }
                else
                {
                    _vm.DeckB.PlayCommand.Execute(null);
                }
            }

            public void Stop()
            {
                if (_vm == null)
                {
                    return;
                }

                if (ActiveDeck == DeckIdentifier.A)
                {
                    _vm.DeckA.StopCommand.Execute(null);
                }
                else
                {
                    _vm.DeckB.StopCommand.Execute(null);
                }
            }
        }

        [Serializable]
        private sealed class QueueDragPayload
        {
            public QueueDragPayload(int sourceIndex)
            {
                SourceIndex = sourceIndex;
            }

            public int SourceIndex { get; }
        }
    }
}
