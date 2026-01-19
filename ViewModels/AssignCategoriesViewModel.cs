using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.ViewModels
{
    public sealed class AssignCategoriesViewModel : ViewModelBase
    {
        private readonly Func<string, string, LibraryCategory> _addCategoryFactory;

        public AssignCategoriesViewModel(
            Guid trackId,
            string trackTitle,
            IEnumerable<LibraryCategory> categories,
            IEnumerable<Guid>? selectedCategoryIds,
            Func<string, string, LibraryCategory> addCategoryFactory)
        {
            TrackId = trackId;
            TrackTitle = trackTitle;
            _addCategoryFactory = addCategoryFactory ?? throw new ArgumentNullException(nameof(addCategoryFactory));

            var selected = new HashSet<Guid>(selectedCategoryIds ?? Array.Empty<Guid>());
            Categories = new ObservableCollection<CategorySelectionItemViewModel>(
                (categories ?? Array.Empty<LibraryCategory>())
                    .OrderBy(static category => category.Type, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static category => category.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(category => CreateCategoryItem(category, selected.Contains(category.Id))));

            foreach (var category in Categories)
            {
                AttachCategoryItem(category);
            }

            AddCategoryCommand = new RelayCommand(_ => AddCategory(), _ => CanAddCategory);
        }

        public Guid TrackId { get; }
        public string TrackTitle { get; }
        public ObservableCollection<CategorySelectionItemViewModel> Categories { get; }

        private string _newCategoryName = string.Empty;
        public string NewCategoryName
        {
            get => _newCategoryName;
            set
            {
                if (SetProperty(ref _newCategoryName, value ?? string.Empty))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _newCategoryType = "General";
        public string NewCategoryType
        {
            get => _newCategoryType;
            set
            {
                if (SetProperty(ref _newCategoryType, string.IsNullOrWhiteSpace(value) ? "General" : value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _lastError = string.Empty;
        public string LastError
        {
            get => _lastError;
            private set => SetProperty(ref _lastError, value ?? string.Empty);
        }

        public ICommand AddCategoryCommand { get; }

        public IReadOnlyCollection<Guid> SelectedCategoryIds => Categories
            .Where(category => category.IsSelected)
            .Select(category => category.CategoryId)
            .ToArray();

        public bool HasSelection => Categories.Any(category => category.IsSelected);

        private bool CanAddCategory => !string.IsNullOrWhiteSpace(NewCategoryName);

        private void AddCategory()
        {
            try
            {
                var category = _addCategoryFactory(NewCategoryName.Trim(), (NewCategoryType ?? "General").Trim());
                var item = CreateCategoryItem(category, isSelected: true);
                AttachCategoryItem(item);
                Categories.Add(item);
                NewCategoryName = string.Empty;
                NewCategoryType = category.Type;
                LastError = string.Empty;
                OnPropertyChanged(nameof(SelectedCategoryIds));
                OnPropertyChanged(nameof(HasSelection));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        private void AttachCategoryItem(CategorySelectionItemViewModel item)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CategorySelectionItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCategoryIds));
                    OnPropertyChanged(nameof(HasSelection));
                }
            };
        }

        private static CategorySelectionItemViewModel CreateCategoryItem(LibraryCategory category, bool isSelected)
        {
            return new CategorySelectionItemViewModel(category ?? throw new ArgumentNullException(nameof(category)), isSelected);
        }
    }

    public sealed class CategorySelectionItemViewModel : ViewModelBase
    {
        private bool _isSelected;

        public CategorySelectionItemViewModel(LibraryCategory category, bool isSelected)
        {
            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            CategoryId = category.Id;
            Name = category.Name;
            Type = category.Type;
            DisplayName = string.IsNullOrWhiteSpace(category.Type)
                ? category.Name
                : $"{category.Name} ({category.Type})";
            _isSelected = isSelected;
        }

        public Guid CategoryId { get; }
        public string Name { get; }
        public string Type { get; }
        public string DisplayName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
