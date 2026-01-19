using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
