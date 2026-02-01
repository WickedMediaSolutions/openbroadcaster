using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public class CategoryItemViewModel
    {
        public CategoryItemViewModel(LibraryCategory category)
        {
            Underlying = category;
            Id = category.Id;
            Name = category.Name;
            Type = category.Type;
            IsToh = LibraryService.IsTohCategory(category.Id);
        }

        public LibraryCategory Underlying { get; }
        public System.Guid Id { get; }
        public string Name { get; }
        public string Type { get; }
        public bool IsToh { get; }
    }
}
