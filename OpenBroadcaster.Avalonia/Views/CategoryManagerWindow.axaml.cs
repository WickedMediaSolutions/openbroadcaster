using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenBroadcaster.Core.Models;
using System.Linq;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class CategoryManagerWindow : Window
    {
        private OpenBroadcaster.Avalonia.ViewModels.CategoryManagerViewModel? _vm;

        public CategoryManagerWindow()
        {
            InitializeComponent();
            this.Opened += CategoryManagerWindow_Opened;
        }

        private void CategoryManagerWindow_Opened(object? sender, System.EventArgs e)
        {
            _vm = this.DataContext as OpenBroadcaster.Avalonia.ViewModels.CategoryManagerViewModel;
            RefreshUI();
            CategoriesList.SelectionChanged += CategoriesList_SelectionChanged;
        }

        private void CategoriesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            var selected = CategoriesList.SelectedItem as OpenBroadcaster.Avalonia.ViewModels.CategoryItemViewModel;
            _vm.SelectedCategory = selected;
            SelectedInfo.Text = selected != null ? $"{selected.Name} ({selected.Type})" : "No category selected";
            EditButton.IsEnabled = selected != null && !selected.IsToh;
            RemoveButton.IsEnabled = selected != null && !selected.IsToh;
        }

        private void RefreshUI()
        {
            if (_vm == null) return;
            CategoriesList.ItemsSource = _vm.Categories;
            SelectedInfo.Text = _vm.SelectedCategory != null ? $"{_vm.SelectedCategory.Name} ({_vm.SelectedCategory.Type})" : "No category selected";
        }

        private async void AddClicked(object? sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var name = NewCategoryName.Text?.Trim();
            var type = NewCategoryType.Text?.Trim() ?? "General";
            if (string.IsNullOrWhiteSpace(name))
            {
                var msg = new SimpleMessageWindow("Invalid Category", "Please enter a category name.");
                await msg.ShowDialog(this);
                return;
            }

            if (_vm.Exists(name, type))
            {
                var msg = new SimpleMessageWindow("Duplicate Category", "A category with that name and type already exists.");
                await msg.ShowDialog(this);
                return;
            }

            try
            {
                _vm.AddCategory(name, type);
                NewCategoryName.Text = string.Empty;
                RefreshUI();
            }
            catch (System.Exception ex)
            {
                var msg = new SimpleMessageWindow("Error", ex.Message);
                await msg.ShowDialog(this);
            }
        }

        private async void EditClicked(object? sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var selected = CategoriesList.SelectedItem as LibraryCategory;
            if (selected == null) return;
            var dlg = new SimpleInputWindow("Edit category name", selected.Name);
            var res = await dlg.ShowDialog<bool?>(this);
            if (res == true)
            {
                var newName = dlg.Result?.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    var msg = new SimpleMessageWindow("Invalid Name", "Category name cannot be empty.");
                    await msg.ShowDialog(this);
                    return;
                }

                if (!string.Equals(newName, selected.Name, System.StringComparison.OrdinalIgnoreCase) && _vm.Exists(newName, selected.Type))
                {
                    var msg = new SimpleMessageWindow("Duplicate Category", "A category with that name and type already exists.");
                    await msg.ShowDialog(this);
                    return;
                }

                try
                {
                    _vm.UpdateCategory(selected.Id, newName, selected.Type);
                    RefreshUI();
                }
                catch (System.Exception ex)
                {
                    var msg = new SimpleMessageWindow("Error", ex.Message);
                    await msg.ShowDialog(this);
                }
            }
        }

        private async void RemoveClicked(object? sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var selected = CategoriesList.SelectedItem as OpenBroadcaster.Avalonia.ViewModels.CategoryItemViewModel;
            if (selected == null) return;
            try
            {
                if (selected.IsToh)
                {
                    var warn = new SimpleMessageWindow("Protected Category", "This is a permanent Top-of-Hour category and cannot be deleted.");
                    await warn.ShowDialog(this);
                    return;
                }

                var confirm = new SimpleConfirmWindow("Confirm Delete", $"Are you sure you want to delete '{selected.Name}'?", "This action cannot be undone.");
                var ok = await confirm.ShowDialog<bool?>(this);
                if (ok == true)
                {
                    _vm.RemoveCategory(selected.Id);
                    RefreshUI();
                }
            }
            catch (System.Exception ex)
            {
                var msg = new SimpleMessageWindow("Error", ex.Message);
                await msg.ShowDialog(this);
            }
        }
    }
}