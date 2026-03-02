using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace OpenBroadcaster.Avalonia.Controls;

public class PowerMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PowerMeter, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<PowerMeter, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<PowerMeter, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<IBrush?> NeedleBrushProperty =
        AvaloniaProperty.Register<PowerMeter, IBrush?>(nameof(NeedleBrush));

    public static readonly StyledProperty<IBrush?> MeterBackgroundProperty =
        AvaloniaProperty.Register<PowerMeter, IBrush?>(nameof(MeterBackground));

    public static readonly StyledProperty<IBrush?> ScaleBrushProperty =
        AvaloniaProperty.Register<PowerMeter, IBrush?>(nameof(ScaleBrush));

    static PowerMeter()
    {
        AffectsRender<PowerMeter>(
            ValueProperty,
            MinimumProperty,
            MaximumProperty,
            NeedleBrushProperty,
            MeterBackgroundProperty,
            ScaleBrushProperty
        );
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? NeedleBrush
    {
        get => GetValue(NeedleBrushProperty);
        set => SetValue(NeedleBrushProperty, value);
    }

    public IBrush? MeterBackground
    {
        get => GetValue(MeterBackgroundProperty);
        set => SetValue(MeterBackgroundProperty, value);
    }

    public IBrush? ScaleBrush
    {
        get => GetValue(ScaleBrushProperty);
        set => SetValue(ScaleBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 0 || height <= 0)
            return;

        // Draw background
        if (MeterBackground != null)
        {
            var bgRect = new Rect(0, 0, width, height);
            context.DrawRectangle(MeterBackground, null, bgRect, 4, 4);
        }

        // Draw theme-aware backlit meter face for readability
        var meterRect = new Rect(2, 2, Math.Max(0, width - 4), Math.Max(0, height - 4));
        var accentColor = ResolveColor(NeedleBrush, Color.FromRgb(24, 204, 0));
        var surfaceColor = ResolveColor(MeterBackground, Color.FromRgb(16, 20, 26));

        var backlightBase = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(WithAlpha(Blend(surfaceColor, Colors.Black, 0.15), 255), 0.0),
                new GradientStop(WithAlpha(Blend(surfaceColor, accentColor, 0.12), 255), 0.55),
                new GradientStop(WithAlpha(Blend(surfaceColor, Colors.Black, 0.20), 255), 1.0)
            }
        };
        context.DrawRectangle(backlightBase, null, meterRect, 3, 3);

        var glowOverlay = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.75, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(WithAlpha(accentColor, 65), 0.0),
                new GradientStop(WithAlpha(accentColor, 28), 0.45),
                new GradientStop(WithAlpha(accentColor, 0), 1.0)
            }
        };
        context.DrawRectangle(glowOverlay, null, meterRect, 3, 3);

        // Calculate center point and radius to fill more of the control bounds
        var centerX = width / 2;
        var centerY = height - 5;
        var arcRadius = Math.Min((width - 8) / 2, height - 10);
        if (arcRadius < 10)
        {
            return;
        }

        // Draw colored zones (like VU meter)
        DrawColoredZones(context, centerX, centerY, arcRadius);

        // Draw scale arc and marks
        if (ScaleBrush != null)
        {
            var scalePen = new Pen(ScaleBrush, 1.1);
            
            // Draw scale marks (0-100 in 10 increments)
            for (int i = 0; i <= 10; i++)
            {
                var angle = 180 + (i * 18); // 180° to 360° (half circle, left to right)
                var radians = angle * Math.PI / 180.0;
                
                var startRadius = arcRadius - (i % 2 == 0 ? 8 : 5);
                var endRadius = arcRadius;
                
                var x1 = centerX + startRadius * Math.Cos(radians);
                var y1 = centerY + startRadius * Math.Sin(radians);
                var x2 = centerX + endRadius * Math.Cos(radians);
                var y2 = centerY + endRadius * Math.Sin(radians);
                
                context.DrawLine(scalePen, new Point(x1, y1), new Point(x2, y2));
            }

            // Draw scale numbers
            var textBrush = ScaleBrush;
            var typeface = new Typeface("Arial");
            
            for (int i = 0; i <= 10; i += 2)
            {
                var angle = 180 + (i * 18);
                var radians = angle * Math.PI / 180.0;
                var textRadius = arcRadius - 14;
                
                var x = centerX + textRadius * Math.Cos(radians);
                var y = centerY + textRadius * Math.Sin(radians);
                
                var text = new FormattedText(
                    (i * 10).ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    8.5,
                    textBrush);
                
                context.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
            }
        }

        // Calculate needle angle based on value (180° = 0%, 360° = 100%)
        var normalizedValue = Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);
        var needleAngle = 180 + (normalizedValue * 180);
        var needleRadians = needleAngle * Math.PI / 180.0;
        var needleLength = arcRadius - 6;

        // Draw needle with proper color (red if over 85%)
        var needleBrush = normalizedValue > 0.85 ? new SolidColorBrush(Colors.Red) : NeedleBrush;
        if (needleBrush != null)
        {
            var needlePen = new Pen(needleBrush, 2);
            var needleEndX = centerX + needleLength * Math.Cos(needleRadians);
            var needleEndY = centerY + needleLength * Math.Sin(needleRadians);
            
            context.DrawLine(needlePen, new Point(centerX, centerY), new Point(needleEndX, needleEndY));
            
            // Draw center pivot
            context.DrawEllipse(needleBrush, needlePen, new Point(centerX, centerY), 3.5, 3.5);
        }
    }

    private void DrawColoredZones(DrawingContext context, double centerX, double centerY, double radius)
    {
        // Draw green zone (0-70%)
        DrawArcZone(context, centerX, centerY, radius - 8, 7, 180, 180 + 126, new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)));
        
        // Draw yellow zone (70-85%)
        DrawArcZone(context, centerX, centerY, radius - 8, 7, 180 + 126, 180 + 153, new SolidColorBrush(Color.FromArgb(80, 255, 255, 0)));
        
        // Draw red zone (85-100%)
        DrawArcZone(context, centerX, centerY, radius - 8, 7, 180 + 153, 360, new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)));
    }

    private void DrawArcZone(DrawingContext context, double centerX, double centerY, double radius, double thickness, double startAngle, double endAngle, IBrush brush)
    {
        var segments = 40;
        var angleStep = (endAngle - startAngle) / segments;
        
        for (int i = 0; i < segments; i++)
        {
            var angle1 = (startAngle + i * angleStep) * Math.PI / 180.0;
            var angle2 = (startAngle + (i + 1) * angleStep) * Math.PI / 180.0;
            
            var innerRadius = radius - thickness / 2;
            var outerRadius = radius + thickness / 2;
            
            var p1 = new Point(centerX + innerRadius * Math.Cos(angle1), centerY + innerRadius * Math.Sin(angle1));
            var p2 = new Point(centerX + outerRadius * Math.Cos(angle1), centerY + outerRadius * Math.Sin(angle1));
            var p3 = new Point(centerX + outerRadius * Math.Cos(angle2), centerY + outerRadius * Math.Sin(angle2));
            var p4 = new Point(centerX + innerRadius * Math.Cos(angle2), centerY + innerRadius * Math.Sin(angle2));
            
            var geometry = new PolylineGeometry(new[] { p1, p2, p3, p4 }, true);
            context.DrawGeometry(brush, null, geometry);
        }
    }

    private static Color ResolveColor(IBrush? brush, Color fallback)
    {
        if (brush is SolidColorBrush solidBrush)
        {
            return solidBrush.Color;
        }

        return fallback;
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        var t = Math.Clamp(amount, 0.0, 1.0);
        var r = (byte)(from.R + ((to.R - from.R) * t));
        var g = (byte)(from.G + ((to.G - from.G) * t));
        var b = (byte)(from.B + ((to.B - from.B) * t));
        return Color.FromRgb(r, g, b);
    }
}
