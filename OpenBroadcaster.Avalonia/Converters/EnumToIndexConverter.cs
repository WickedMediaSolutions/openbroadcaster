using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OpenBroadcaster.Avalonia.Converters
{
    /// <summary>
    /// Converts an enum value to its integer index and vice versa.
    /// </summary>
    public sealed class EnumToIndexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return 0;
            }

            return System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return 0;
            }

            var index = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);

            if (targetType.IsEnum)
            {
                var values = Enum.GetValues(targetType);
                if (index >= 0 && index < values.Length)
                {
                    return values.GetValue(index) ?? Activator.CreateInstance(targetType);
                }
            }

            return index;
        }
    }
}
