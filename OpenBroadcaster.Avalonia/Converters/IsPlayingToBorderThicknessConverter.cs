using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OpenBroadcaster.Avalonia.Converters
{
    public class IsPlayingToBorderThicknessConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPlaying && isPlaying)
            {
                return new Thickness(4);
            }
            return new Thickness(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
