using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OpenBroadcaster.Avalonia.Converters
{
    public sealed class BoolToOpacityConverter : IValueConverter
    {
        public static readonly BoolToOpacityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isOn = value is bool b && b;

            if (parameter is string p)
            {
                var parts = p.Split('|');
                if (parts.Length == 2
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var onValue)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offValue))
                {
                    return isOn ? onValue : offValue;
                }
            }

            return isOn ? 1.0 : 0.35;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
