using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OpenBroadcaster.Avalonia.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public static readonly BoolToBrushConverter Instance = new();

        public IBrush TrueBrush { get; set; } = Brushes.LightGoldenrodYellow;
        public IBrush FalseBrush { get; set; } = Brushes.Transparent;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            // If parameter is provided, use color pair format
            if (value is bool boolValue && parameter is string colorPair)
            {
                var colors = colorPair.Split('|');
                if (colors.Length == 2)
                {
                    var trueColor = colors[0].Trim();
                    var falseColor = colors[1].Trim();
                    var color = boolValue ? trueColor : falseColor;
                    return new SolidColorBrush(global::Avalonia.Media.Color.Parse(color));
                }
            }
            
            // Otherwise use TrueBrush/FalseBrush properties
            if (value is bool b && b) return TrueBrush;
            return FalseBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotSupportedException();
        }
    }
}
