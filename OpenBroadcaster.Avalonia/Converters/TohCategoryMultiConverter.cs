using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using OpenBroadcaster.Avalonia.ViewModels;

namespace OpenBroadcaster.Avalonia.Converters
{
    /// <summary>
    /// Multi-binding converter that maps CategoryId + options to a selected option and back.
    /// </summary>
    public sealed class TohCategoryMultiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2) return null;

            var id = values[0] is Guid guid ? guid : Guid.Empty;
            var options = values[1] as IEnumerable<TohCategoryOption>;
            if (options == null) return null;

            return options.FirstOrDefault(opt => opt.CategoryId == id);
        }

        public object? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            if (targetTypes.Length == 0) return null;

            if (value is TohCategoryOption option)
            {
                return new object?[] { option.CategoryId, BindingOperations.DoNothing };
            }

            return new object?[] { Guid.Empty, BindingOperations.DoNothing };
        }
    }
}
