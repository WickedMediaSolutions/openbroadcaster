using System.Collections.ObjectModel;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Models;
using System.Linq;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public class CategoryManagerViewModel
    {
        private readonly LibraryService _libraryService;

        public CategoryManagerViewModel(LibraryService libraryService)
        {
            _libraryService = libraryService;
            Categories = new ObservableCollection<CategoryItemViewModel>(_libraryService.GetCategories().Select(c => new CategoryItemViewModel(c)));
        }

        public System.Collections.ObjectModel.ObservableCollection<CategoryItemViewModel> Categories { get; }

        public CategoryItemViewModel? SelectedCategory { get; set; }
        
        public void Refresh()
        {
            Categories.Clear();
            foreach (var c in _libraryService.GetCategories())
            {
                Categories.Add(new CategoryItemViewModel(c));
            }
        }
        
        public bool Exists(string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return Categories.Any(c => string.Equals(c.Name?.Trim(), name.Trim(), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Type ?? "General", type ?? "General", System.StringComparison.OrdinalIgnoreCase));
        }

        public void AddCategory(string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new System.ArgumentException("Category name cannot be empty.", nameof(name));
            if (Exists(name, type)) throw new System.InvalidOperationException("A category with that name and type already exists.");
            var cat = _libraryService.AddCategory(name.Trim(), type ?? "General");
            Categories.Add(new CategoryItemViewModel(cat));
        }

        public void UpdateCategory(System.Guid id, string name, string type)
        {
            var updated = _libraryService.UpdateCategory(id, name, type);
            var idx = Categories.ToList().FindIndex(c => c.Id == id);
            if (idx >= 0)
            {
                Categories[idx] = new CategoryItemViewModel(updated);
            }
        }

        public void RemoveCategory(System.Guid id)
        {
            if (_libraryService.RemoveCategory(id))
            {
                var existing = Categories.FirstOrDefault(c => c.Id == id);
                if (existing != null) Categories.Remove(existing);
            }
        }
    }
}