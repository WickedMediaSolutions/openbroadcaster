using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public sealed partial class ManageCategoriesWindow : Window
    {
        private readonly ManageCategoriesViewModel _viewModel;

        public ManageCategoriesWindow(LibraryService libraryService)
        {
            InitializeComponent();
            _viewModel = new ManageCategoriesViewModel(libraryService);
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    internal sealed class ManageCategoriesViewModel : INotifyPropertyChanged
    {
        private readonly LibraryService _libraryService;
        private string _newCategoryName = string.Empty;
        private string _newCategoryType = "Music";
        private CategoryItemViewModel? _selectedCategory;
        private bool _isEditing;

        public ManageCategoriesViewModel(LibraryService libraryService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            Categories = new ObservableCollection<CategoryItemViewModel>();
            
            AddCategoryCommand = new RelayCommand(_ =>
            {
                if (IsEditing) SaveEdit(); else AddCategory();
            }, _ => CanAddCategory);
            RemoveCategoryCommand = new RelayCommand(async _ => await RemoveCategoryAsync(), _ => SelectedCategory != null && !IsEditing && !SelectedCategory.IsTohCategory);
            EditCategoryCommand = new RelayCommand(_ => StartEdit(), _ => SelectedCategory != null && !IsEditing && !SelectedCategory.IsTohCategory);
            CancelEditCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
            
            LoadCategories();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<CategoryItemViewModel> Categories { get; }

        public string NewCategoryName
        {
            get => _newCategoryName;
            set
            {
                if (_newCategoryName != value)
                {
                    _newCategoryName = value;
                    OnPropertyChanged(nameof(NewCategoryName));
                    OnPropertyChanged(nameof(CanAddCategory));
                }
            }
        }

        public string NewCategoryType
        {
            get => _newCategoryType;
            set
            {
                if (_newCategoryType != value)
                {
                    _newCategoryType = value;
                    OnPropertyChanged(nameof(NewCategoryType));
                }
            }
        }

        public CategoryItemViewModel? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged(nameof(SelectedCategory));
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (_isEditing != value)
                {
                    _isEditing = value;
                    OnPropertyChanged(nameof(IsEditing));
                    OnPropertyChanged(nameof(AddButtonText));
                }
            }
        }

        public string AddButtonText => IsEditing ? "Save" : "Add";

        public RelayCommand AddCategoryCommand { get; }
        public RelayCommand RemoveCategoryCommand { get; }
        public RelayCommand EditCategoryCommand { get; }
        public RelayCommand CancelEditCommand { get; }

        private bool CanAddCategory => !string.IsNullOrWhiteSpace(NewCategoryName);

        private void AddCategory()
        {
            try
            {
                var category = _libraryService.AddCategory(NewCategoryName.Trim(), NewCategoryType.Trim());
                Categories.Add(new CategoryItemViewModel(category));
                NewCategoryName = string.Empty;
                NewCategoryType = "Music";
            }
            catch (Exception ex)
            {
                _ = UiServices.ShowWarningAsync("Category Error", $"Failed to add category: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task RemoveCategoryAsync()
        {
            if (SelectedCategory == null)
            {
                return;
            }

            var confirmed = await UiServices.ShowConfirmAsync(
                "Confirm Remove",
                $"Remove category '{SelectedCategory.Name}'? Tracks will keep their other categories.");

            if (confirmed)
            {
                _libraryService.RemoveCategory(SelectedCategory.Id);
                Categories.Remove(SelectedCategory);
            }
        }

        private void StartEdit()
        {
            if (SelectedCategory == null) return;
            NewCategoryName = SelectedCategory.Name;
            NewCategoryType = SelectedCategory.Type;
            IsEditing = true;
        }

        private void SaveEdit()
        {
            if (SelectedCategory == null) return;
            
            try
            {
                var updated = _libraryService.UpdateCategory(
                    SelectedCategory.Id,
                    NewCategoryName.Trim(),
                    NewCategoryType.Trim());
                
                var index = Categories.IndexOf(SelectedCategory);
                Categories[index] = new CategoryItemViewModel(updated);
                SelectedCategory = Categories[index];
                
                NewCategoryName = string.Empty;
                NewCategoryType = "Music";
                IsEditing = false;
            }
            catch (Exception ex)
            {
                _ = UiServices.ShowWarningAsync("Category Error", $"Failed to update category: {ex.Message}");
            }
        }

        private void CancelEdit()
        {
            NewCategoryName = string.Empty;
            NewCategoryType = "Music";
            IsEditing = false;
        }

        private void LoadCategories()
        {
            Categories.Clear();
            var categories = _libraryService.GetCategories();
            var reserved = new[] { "All Categories", "All", "Uncategorized" };
            
            foreach (var cat in categories)
            {
                if (!reserved.Any(r => string.Equals(r, cat.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Categories.Add(new CategoryItemViewModel(cat));
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal sealed class CategoryItemViewModel
    {
        public CategoryItemViewModel(LibraryCategory category)
        {
            Id = category.Id;
            Name = category.Name;
            Type = category.Type;
            IsTohCategory = LibraryService.IsTohCategory(category.Id);
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Type { get; }
        public bool IsTohCategory { get; }
    }
}
