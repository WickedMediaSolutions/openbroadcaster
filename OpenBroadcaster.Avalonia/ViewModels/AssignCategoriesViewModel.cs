using System.Collections.ObjectModel;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Models;
using System.Linq;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public class AssignCategoryItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public bool IsToh { get; set; }
    }

    public class AssignCategoriesViewModel
    {
        private readonly LibraryService _libraryService;
        private readonly Guid _trackId;

        public AssignCategoriesViewModel(LibraryService libraryService, Guid trackId)
        {
            _libraryService = libraryService;
            _trackId = trackId;
            var all = _libraryService.GetCategories();
            var track = _libraryService.GetTrack(trackId);
            var assigned = track?.CategoryIds ?? System.Array.Empty<Guid>();
            Categories = new ObservableCollection<AssignCategoryItem>(all.Select(c => new AssignCategoryItem { Id = c.Id, Name = c.Name, IsChecked = assigned.Contains(c.Id), IsToh = OpenBroadcaster.Core.Services.LibraryService.IsTohCategory(c.Id) }));
        }

        public ObservableCollection<AssignCategoryItem> Categories { get; }

        public void Apply()
        {
            var picked = Categories.Where(c => c.IsChecked).Select(c => c.Id).ToArray();
            _libraryService.AssignCategories(_trackId, picked);
        }
    }
}