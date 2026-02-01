using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Views
{
    public sealed partial class ImportCategorySelectorWindow : Window
    {
        private readonly ImportCategorySelectorViewModel _viewModel;

        public ImportCategorySelectorWindow(IEnumerable<LibraryCategory> availableCategories)
        {
            InitializeComponent();
            _viewModel = new ImportCategorySelectorViewModel(availableCategories);
            DataContext = _viewModel;
        }

        public IReadOnlyList<Guid> SelectedCategoryIds => _viewModel.SelectedCategoryIds;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnImportClick(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }

    internal sealed class ImportCategorySelectorViewModel : INotifyPropertyChanged
    {
        public ImportCategorySelectorViewModel(IEnumerable<LibraryCategory> availableCategories)
        {
            Categories = new ObservableCollection<SelectableCategoryViewModel>(
                availableCategories.Select(c => new SelectableCategoryViewModel(c)));
        }

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public ObservableCollection<SelectableCategoryViewModel> Categories { get; }

        public IReadOnlyList<Guid> SelectedCategoryIds => Categories
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .ToList();
    }

    internal sealed class SelectableCategoryViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public SelectableCategoryViewModel(LibraryCategory category)
        {
            Id = category.Id;
            var label = string.IsNullOrWhiteSpace(category.Type)
                ? category.Name
                : $"{category.Name} ({category.Type})";
            DisplayName = label;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Id { get; }
        public string DisplayName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }
    }
}
