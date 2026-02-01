using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public static class TohCategoryOptionsProvider
    {
        public static IReadOnlyList<TohCategoryOption> Options { get; set; } = Array.Empty<TohCategoryOption>();

        public static TohCategoryOption? Find(Guid id)
        {
            return Options.FirstOrDefault(opt => opt.CategoryId == id);
        }
    }
}
