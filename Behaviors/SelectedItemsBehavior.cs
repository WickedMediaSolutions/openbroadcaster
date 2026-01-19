using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace OpenBroadcaster.Behaviors
{
    public static class SelectedItemsBehavior
    {
        public static readonly DependencyProperty BindableSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "BindableSelectedItems",
                typeof(IList),
                typeof(SelectedItemsBehavior),
                new PropertyMetadata(null, OnBindableSelectedItemsChanged));

        public static void SetBindableSelectedItems(DependencyObject element, IList value)
        {
            element.SetValue(BindableSelectedItemsProperty, value);
        }

        public static IList GetBindableSelectedItems(DependencyObject element)
        {
            return (IList)element.GetValue(BindableSelectedItemsProperty);
        }

        private static void OnBindableSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.ListBox listBox)
            {
                listBox.SelectionChanged -= ListBox_SelectionChanged;
                listBox.SelectionChanged += ListBox_SelectionChanged;
                SyncSelectedItems(listBox, e.NewValue as IList);
            }
        }

        private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox)
            {
                var bindableSelectedItems = GetBindableSelectedItems(listBox);
                if (bindableSelectedItems == null) return;
                bindableSelectedItems.Clear();
                foreach (var item in listBox.SelectedItems)
                {
                    bindableSelectedItems.Add(item);
                }
            }
        }

        private static void SyncSelectedItems(System.Windows.Controls.ListBox listBox, IList? selectedItems)
        {
            if (selectedItems == null) return;
            listBox.SelectedItems.Clear();
            foreach (var item in selectedItems)
            {
                listBox.SelectedItems.Add(item);
            }
        }
    }
}
