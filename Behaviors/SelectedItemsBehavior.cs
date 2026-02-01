using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenBroadcaster.Behaviors
{
    public sealed class SelectedItemsBehavior
    {
        public static readonly AttachedProperty<IList?> BindableSelectedItemsProperty =
            AvaloniaProperty.RegisterAttached<SelectedItemsBehavior, ListBox, IList?>(
                "BindableSelectedItems");

        static SelectedItemsBehavior()
        {
            BindableSelectedItemsProperty.Changed.AddClassHandler<ListBox>(OnBindableSelectedItemsChanged);
        }

        public static void SetBindableSelectedItems(AvaloniaObject element, IList? value)
        {
            element.SetValue(BindableSelectedItemsProperty, value);
        }

        public static IList? GetBindableSelectedItems(AvaloniaObject element)
        {
            return element.GetValue(BindableSelectedItemsProperty);
        }

        private static void OnBindableSelectedItemsChanged(ListBox listBox, AvaloniaPropertyChangedEventArgs e)
        {
            listBox.SelectionChanged -= ListBox_SelectionChanged;
            listBox.SelectionChanged += ListBox_SelectionChanged;
            SyncSelectedItems(listBox, e.NewValue as IList);
        }

        private static void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox)
            {
                return;
            }

            var bindableSelectedItems = GetBindableSelectedItems(listBox);
            if (bindableSelectedItems == null)
            {
                return;
            }

            bindableSelectedItems.Clear();
            foreach (var item in listBox.SelectedItems)
            {
                bindableSelectedItems.Add(item);
            }
        }

        private static void SyncSelectedItems(ListBox listBox, IList? selectedItems)
        {
            if (selectedItems == null)
            {
                return;
            }

            listBox.SelectedItems.Clear();
            foreach (var item in selectedItems)
            {
                listBox.SelectedItems.Add(item);
            }
        }
    }
}
