using System;
using System.Globalization;
using System.Windows.Data;

namespace OpenBroadcaster.Converters
{
    /// <summary>
    /// Converts an enum value to its integer index and vice versa.
    /// </summary>
    public sealed class EnumToIndexConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return 0;
            }

            return System.Convert.ToInt32(value);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return 0;
            }

            var index = System.Convert.ToInt32(value);
            
            // Return the enum value at the given index
            if (targetType.IsEnum)
            {
                var values = Enum.GetValues(targetType);
                if (index >= 0 && index < values.Length)
                {
                    return values.GetValue(index) ?? 0;
                }
            }

            return index;
        }
    }
}
