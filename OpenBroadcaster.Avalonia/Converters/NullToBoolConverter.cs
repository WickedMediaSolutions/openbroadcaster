using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OpenBroadcaster.Avalonia.Converters
{
    public class NullToBoolConverter : IValueConverter
    {
        public static readonly NullToBoolConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool present = value != null;
            try
            {
                if (parameter is string p && string.Equals(p, "inverse", StringComparison.OrdinalIgnoreCase))
                {
                    return !present;
                }
            }
            catch { }
            return present;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
