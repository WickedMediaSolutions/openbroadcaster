using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace OpenBroadcaster.Avalonia.Controls;

public class RotaryKnob : Control
{
    private Point _lastPoint;
    private bool _isDragging;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RotaryKnob, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RotaryKnob, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RotaryKnob, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<IBrush?> KnobBrushProperty =
        AvaloniaProperty.Register<RotaryKnob, IBrush?>(nameof(KnobBrush));

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<RotaryKnob, IBrush?>(nameof(IndicatorBrush));

    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<RotaryKnob, IBrush?>(nameof(BackgroundBrush));

    static RotaryKnob()
    {
        AffectsRender<RotaryKnob>(
            ValueProperty,
            MinimumProperty,
            MaximumProperty,
            KnobBrushProperty,
            IndicatorBrushProperty,
            BackgroundBrushProperty
        );

        ValueProperty.Changed.AddClassHandler<RotaryKnob>((knob, e) =>
        {
            knob.CoerceValue();
        });
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

    public IBrush? KnobBrush
    {
        get => GetValue(KnobBrushProperty);
        set => SetValue(KnobBrushProperty, value);
    }

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    private void CoerceValue()
    {
        var newValue = Math.Clamp(Value, Minimum, Maximum);
        if (Math.Abs(Value - newValue) > 0.001)
        {
            SetValue(ValueProperty, newValue);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isDragging = true;
        _lastPoint = e.GetPosition(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        
        if (!_isDragging)
            return;

        var currentPoint = e.GetPosition(this);
        var deltaY = _lastPoint.Y - currentPoint.Y;
        
        var range = Maximum - Minimum;
        var sensitivity = range / 100.0; // 100 pixels for full range
        var delta = deltaY * sensitivity;
        
        Value = Math.Clamp(Value + delta, Minimum, Maximum);
        
        _lastPoint = currentPoint;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        var size = Math.Min(width, height);
        var centerX = width / 2;
        var centerY = height / 2;
        var radius = size / 2 - 4;

        if (size <= 0)
            return;

        // Draw background
        if (BackgroundBrush != null)
        {
            context.DrawEllipse(BackgroundBrush, null, new Point(centerX, centerY), radius + 2, radius + 2);
        }

        // Draw knob body with gradient effect
        if (KnobBrush != null)
        {
            var gradientBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Colors.White, 0.0) { Color = Color.FromArgb(60, 255, 255, 255) },
                    new GradientStop((KnobBrush as SolidColorBrush)?.Color ?? Colors.Gray, 0.5),
                    new GradientStop(Colors.Black, 1.0) { Color = Color.FromArgb(40, 0, 0, 0) }
                },
                Center = new RelativePoint(0.3, 0.3, RelativeUnit.Relative)
            };
            
            context.DrawEllipse(gradientBrush, new Pen(KnobBrush, 2), new Point(centerX, centerY), radius, radius);
        }

        // Calculate indicator angle
        var normalizedValue = (Value - Minimum) / (Maximum - Minimum);
        var angle = -135 + (normalizedValue * 270); // -135° to +135° (270° total)
        var radians = angle * Math.PI / 180.0;

        // Draw indicator line
        if (IndicatorBrush != null)
        {
            var indicatorLength = radius * 0.7;
            var indicatorX = centerX + indicatorLength * Math.Cos(radians);
            var indicatorY = centerY + indicatorLength * Math.Sin(radians);
            
            var pen = new Pen(IndicatorBrush, 3);
            context.DrawLine(pen, new Point(centerX, centerY), new Point(indicatorX, indicatorY));
        }

        // Draw center dot
        if (IndicatorBrush != null)
        {
            context.DrawEllipse(IndicatorBrush, null, new Point(centerX, centerY), 3, 3);
        }

        // Draw scale marks
        if (KnobBrush != null)
        {
            var scalePen = new Pen(KnobBrush, 1);
            for (int i = 0; i <= 10; i++)
            {
                var markAngle = -135 + (i * 27); // Distribute marks across 270°
                var markRadians = markAngle * Math.PI / 180.0;
                var markRadius = radius + 2;
                var markX = centerX + markRadius * Math.Cos(markRadians);
                var markY = centerY + markRadius * Math.Sin(markRadians);
                
                context.DrawEllipse(KnobBrush, null, new Point(markX, markY), 1.5, 1.5);
            }
        }
    }
}
