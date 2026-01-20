using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenBroadcaster.Converters
{
    /// <summary>
    /// Converts a boolean value to Visibility, inverted.
    /// True => Collapsed, False => Visible.
    /// </summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            return flag ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
