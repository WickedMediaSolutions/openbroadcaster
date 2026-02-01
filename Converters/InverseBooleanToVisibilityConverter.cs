using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

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
            return !flag;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
