using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenBroadcaster.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class ImportCategorySelectorWindow : Window
    {
        private readonly List<SelectableCategoryItem> _items = new();

        public ImportCategorySelectorWindow()
        {
            InitializeComponent();
        }

        public ImportCategorySelectorWindow(IEnumerable<LibraryCategory> categories) : this()
        {
            foreach (var cat in categories)
            {
                var item = new SelectableCategoryItem(cat);
                _items.Add(item);
            }
            BuildUI();
        }

        public IReadOnlyList<Guid> SelectedCategoryIds => _items
            .Where(i => i.IsSelected)
            .Select(i => i.Id)
            .ToList();

        private void BuildUI()
        {
            CategoriesPanel.Children.Clear();
            foreach (var item in _items)
            {
                var cb = new CheckBox
                {
                    Content = item.DisplayName,
                    IsChecked = item.IsSelected
                };
                cb.IsCheckedChanged += (_, __) => item.IsSelected = cb.IsChecked == true;
                CategoriesPanel.Children.Add(cb);
            }
        }

        private void ImportClicked(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void CancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private class SelectableCategoryItem
        {
            public SelectableCategoryItem(LibraryCategory category)
            {
                Id = category.Id;
                DisplayName = string.IsNullOrWhiteSpace(category.Type)
                    ? category.Name
                    : $"{category.Name} ({category.Type})";
            }

            public Guid Id { get; }
            public string DisplayName { get; }
            public bool IsSelected { get; set; }
        }
    }
}
