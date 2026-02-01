using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OpenBroadcaster.Avalonia.ViewModels;

namespace OpenBroadcaster.Avalonia.Converters
{
    /// <summary>
    /// Converts between a TOH CategoryId (Guid) and a TohCategoryOption item.
    /// </summary>
    public sealed class TohCategorySelectionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Guid id)
            {
                return TohCategoryOptionsProvider.Find(id);
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TohCategoryOption option)
            {
                return option.CategoryId;
            }

            return Guid.Empty;
        }
    }
}
