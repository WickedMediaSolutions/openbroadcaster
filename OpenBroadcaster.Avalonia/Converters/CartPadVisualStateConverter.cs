using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
namespace OpenBroadcaster.Avalonia.Converters
{
    public sealed class CartPadVisualStateConverter : IValueConverter
    {
        public static readonly CartPadVisualStateConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var mode = (parameter as string ?? "background").ToLowerInvariant();

            var isPlaying = false;
            double remainingSeconds = -1;

            if (value is bool boolValue)
            {
                isPlaying = boolValue;
            }
            else if (value is TimeSpan timeSpan)
            {
                remainingSeconds = timeSpan.TotalSeconds;
                isPlaying = remainingSeconds > 0;
            }
            else if (value is string remainingText)
            {
                isPlaying = !string.IsNullOrWhiteSpace(remainingText);
                if (isPlaying)
                {
                    var trimmed = remainingText.Trim();
                    if (trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                    {
                        trimmed = trimmed.Substring(0, trimmed.Length - 1);
                    }

                    if (double.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
                    {
                        remainingSeconds = seconds;
                    }
                }
            }

            if (mode == "thickness")
            {
                return isPlaying ? new Thickness(3) : new Thickness(2);
            }

            if (mode == "border")
            {
                if (isPlaying)
                {
                    return ResolveBrush("ObBrushAccent", Brushes.DodgerBlue);
                }

                return ResolveBrush("ObBrushBorder", Brushes.Gray);
            }

            // background mode
            var surface = ResolveBrush("ObBrushSurface", Brushes.Transparent);
            var panel = ResolveBrush("ObBrushPanel", Brushes.Transparent);
            var accent = ResolveBrush("ObBrushAccent", Brushes.DodgerBlue);
            var accentSoft = ResolveBrush("ObBrushAccentSoft", Brushes.Transparent);

            if (!isPlaying)
            {
                return surface;
            }

            var isCriticalWindow = remainingSeconds > 0 && remainingSeconds <= 5;
            if (isCriticalWindow)
            {
                var flashOn = ((int)(DateTime.UtcNow.Ticks / TimeSpan.FromMilliseconds(300).Ticks)) % 2 == 0;
                var accentColor = ResolveColor(accent, Colors.LimeGreen);
                return flashOn
                    ? BuildGradient(
                        accentColor,
                        accentColor,
                        ResolveColor(surface, Colors.Black),
                        0.90,
                        0.75,
                        0.50)
                    : BuildGradient(
                        accentColor,
                        ResolveColor(surface, Colors.Black),
                        ResolveColor(panel, Colors.Black),
                        0.55,
                        0.40,
                        0.30);
            }

            var playingAccentColor = ResolveColor(accent, Colors.LimeGreen);
            return BuildGradient(
                playingAccentColor,
                playingAccentColor,
                ResolveColor(surface, Colors.Black),
                0.62,
                0.42,
                0.30);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static IBrush ResolveBrush(string key, IBrush fallback)
        {
            if (global::Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var resource) == true
                && resource is IBrush brush)
            {
                return brush;
            }

            return fallback;
        }

        private static IBrush WithOpacity(IBrush brush, double opacity)
        {
            if (brush is ISolidColorBrush solid)
            {
                return new SolidColorBrush(solid.Color, opacity);
            }

            return brush;
        }

        private static Color ResolveColor(IBrush brush, Color fallback)
        {
            if (brush is ISolidColorBrush solid)
            {
                return solid.Color;
            }

            return fallback;
        }

        private static IBrush BuildGradient(Color top, Color mid, Color bottom, double topOpacity, double midOpacity, double bottomOpacity)
        {
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb((byte)(Math.Clamp(topOpacity, 0, 1) * 255), top.R, top.G, top.B), 0.0),
                    new GradientStop(Color.FromArgb((byte)(Math.Clamp(midOpacity, 0, 1) * 255), mid.R, mid.G, mid.B), 0.55),
                    new GradientStop(Color.FromArgb((byte)(Math.Clamp(bottomOpacity, 0, 1) * 255), bottom.R, bottom.G, bottom.B), 1.0)
                }
            };
        }
    }
}
