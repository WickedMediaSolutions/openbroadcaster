using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public partial class ManageCategoriesWindow : Window
    {
        private readonly ManageCategoriesViewModel _viewModel;

        public ManageCategoriesWindow(LibraryService libraryService)
        {
            InitializeComponent();
            _viewModel = new ManageCategoriesViewModel(libraryService);
            DataContext = _viewModel;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
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
            RemoveCategoryCommand = new RelayCommand(_ => RemoveCategory(), _ => SelectedCategory != null && !IsEditing && !SelectedCategory.IsTohCategory);
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

        public ICommand AddCategoryCommand { get; }
        public ICommand RemoveCategoryCommand { get; }
        public ICommand EditCategoryCommand { get; }
        public ICommand CancelEditCommand { get; }

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
                System.Windows.MessageBox.Show($"Failed to add category: {ex.Message}", "Category Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private void RemoveCategory()
        {
            if (SelectedCategory == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Remove category '{SelectedCategory.Name}'? Tracks will keep their other categories.",
                "Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
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
                System.Windows.MessageBox.Show($"Failed to update category: {ex.Message}", "Category Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
            
            // Filter out reserved names that shouldn't appear in the management UI
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
