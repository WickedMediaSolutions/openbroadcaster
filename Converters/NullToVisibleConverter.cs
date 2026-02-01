using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace OpenBroadcaster.Converters
{
    /// <summary>
    /// Returns Visible when value is null, Collapsed otherwise.
    /// Used for showing placeholder content when actual content is null.
    /// </summary>
    public sealed class NullToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
