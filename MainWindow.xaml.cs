using System;
using System.Linq;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Avalonia.Controls;

namespace OpenBroadcaster
{
    public sealed partial class MainWindow : Window
    {
    }
}
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
