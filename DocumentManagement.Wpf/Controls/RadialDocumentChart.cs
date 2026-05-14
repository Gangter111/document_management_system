using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DocumentManagement.Wpf.Controls;

public class RadialDocumentChart : Canvas
{
    public const string TotalMetric = "Total";
    public const string IssuedMetric = "Issued";
    public const string EffectiveMetric = "Effective";
    public const string ExpiredMetric = "Expired";

    private static readonly Typeface TitleTypeface = new("Segoe UI");
    private static readonly Typeface HeavyTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
    private static readonly Typeface BoldTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Brush SurfaceBrush = Freeze(new SolidColorBrush(Color.FromArgb(218, 255, 255, 255)));
    private static readonly Brush MutedRingBrush = Freeze(new SolidColorBrush(Color.FromArgb(34, 96, 165, 250)));
    private static readonly Brush OuterTickBrush = Freeze(new SolidColorBrush(Color.FromArgb(44, 37, 99, 235)));
    private static readonly Brush SoftBlueBrush = Freeze(new SolidColorBrush(Color.FromArgb(74, 125, 211, 252)));
    private static readonly Brush CenterTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(7, 26, 100)));
    private static readonly Brush GreyBrush = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));
    private static readonly Brush BlueBrush = Freeze(new SolidColorBrush(Color.FromRgb(37, 99, 235)));
    private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(16, 185, 129)));
    private static readonly Brush RedBrush = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
    private static readonly Brush CyanWashBrush = Freeze(CreateAmbientWashBrush());
    private static readonly Brush ArcShadowBrush = Freeze(new SolidColorBrush(Color.FromArgb(42, 15, 23, 42)));
    private static readonly Brush ArcLowerEdgeBrush = Freeze(new SolidColorBrush(Color.FromArgb(58, 7, 26, 100)));
    private static readonly Brush ArcSpecularBrush = Freeze(new SolidColorBrush(Color.FromArgb(176, 255, 255, 255)));
    private static readonly Brush TrackBrush = Freeze(new SolidColorBrush(Color.FromArgb(92, 148, 163, 184)));
    private static readonly Brush TrackDepthBrush = Freeze(new SolidColorBrush(Color.FromArgb(24, 15, 23, 42)));
    private static readonly Brush TotalArcBrush = Freeze(CreateGradient("#FFFFFF", "#E2E8F0", "#CBD5E1", "#94A3B8"));
    private static readonly Brush IssuedArcBrush = Freeze(CreateGradient("#BAE6FD", "#38BDF8", "#0EA5E9", "#1D4ED8"));
    private static readonly Brush EffectiveArcBrush = Freeze(CreateGradient("#BBF7D0", "#4ADE80", "#10B981", "#047857"));
    private static readonly Brush ExpiredArcBrush = Freeze(CreateGradient("#FECACA", "#FB7185", "#F43F5E", "#DC2626"));

    public static readonly DependencyProperty TotalDocumentsProperty =
        DependencyProperty.Register(nameof(TotalDocuments), typeof(int), typeof(RadialDocumentChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IssuedDocumentsProperty =
        DependencyProperty.Register(nameof(IssuedDocuments), typeof(int), typeof(RadialDocumentChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EffectiveDocumentsProperty =
        DependencyProperty.Register(nameof(EffectiveDocuments), typeof(int), typeof(RadialDocumentChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ExpiredDocumentsProperty =
        DependencyProperty.Register(nameof(ExpiredDocuments), typeof(int), typeof(RadialDocumentChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedMetricProperty =
        DependencyProperty.Register(nameof(SelectedMetric), typeof(string), typeof(RadialDocumentChart),
            new FrameworkPropertyMetadata(TotalMetric, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoveredMetricProperty =
        DependencyProperty.Register(nameof(HoveredMetric), typeof(string), typeof(RadialDocumentChart),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public int TotalDocuments
    {
        get => (int)GetValue(TotalDocumentsProperty);
        set => SetValue(TotalDocumentsProperty, value);
    }

    public int IssuedDocuments
    {
        get => (int)GetValue(IssuedDocumentsProperty);
        set => SetValue(IssuedDocumentsProperty, value);
    }

    public int EffectiveDocuments
    {
        get => (int)GetValue(EffectiveDocumentsProperty);
        set => SetValue(EffectiveDocumentsProperty, value);
    }

    public int ExpiredDocuments
    {
        get => (int)GetValue(ExpiredDocumentsProperty);
        set => SetValue(ExpiredDocumentsProperty, value);
    }

    public string SelectedMetric
    {
        get => (string)GetValue(SelectedMetricProperty);
        set => SetValue(SelectedMetricProperty, value);
    }

    public string HoveredMetric
    {
        get => (string)GetValue(HoveredMetricProperty);
        set => SetValue(HoveredMetricProperty, value);
    }

    public RadialDocumentChart()
    {
        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var metric = HitTestMetric(e.GetPosition(this)) ?? string.Empty;
        if (string.Equals(metric, HoveredMetric, StringComparison.Ordinal))
        {
            return;
        }

        HoveredMetric = metric;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (string.IsNullOrWhiteSpace(HoveredMetric))
        {
            return;
        }

        HoveredMetric = string.Empty;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var metric = HitTestMetric(e.GetPosition(this));
        if (!string.IsNullOrWhiteSpace(metric))
        {
            SelectedMetric = metric;
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var size = Math.Max(1, Math.Min(ActualWidth, ActualHeight));
        var scale = size / 600.0;
        var cx = ActualWidth / 2.0;
        var cy = ActualHeight / 2.0;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        DrawAmbientShell(dc, cx, cy, scale);
        DrawTicks(dc, cx, cy, scale, dpi);

        DrawOuterTotalRing(dc, cx, cy, 224 * scale, 35 * scale);
        DrawMetricArc(dc, cx, cy, 166 * scale, IssuedPercent, IssuedMetric, IssuedArcBrush, BlueBrush, "Phát hành", scale, dpi);
        DrawMetricArc(dc, cx, cy, 123 * scale, EffectivePercent, EffectiveMetric, EffectiveArcBrush, GreenBrush, "Còn hiệu lực", scale, dpi);
        DrawMetricArc(dc, cx, cy, 81 * scale, ExpiredPercent, ExpiredMetric, ExpiredArcBrush, RedBrush, "Hết hiệu lực", scale, dpi);
        DrawCenterHub(dc, cx, cy, scale, dpi);
    }

    private double IssuedPercent => Percent(IssuedDocuments);

    private double EffectivePercent => Percent(EffectiveDocuments);

    private double ExpiredPercent => Percent(ExpiredDocuments);

    private double Percent(int value)
    {
        if (TotalDocuments <= 0)
        {
            return 0;
        }

        return Math.Clamp(value / (double)TotalDocuments * 100.0, 0, 100);
    }

    private void DrawAmbientShell(DrawingContext dc, double cx, double cy, double scale)
    {
        dc.DrawEllipse(CyanWashBrush, null, new Point(cx, cy + 10 * scale), 214 * scale, 214 * scale);
        dc.PushOpacity(0.16);
        dc.DrawEllipse(ArcShadowBrush, null, new Point(cx, cy + 18 * scale), 212 * scale, 196 * scale);
        dc.Pop();

        DrawEllipse(dc, cx, cy, 269 * scale, null, new Pen(MutedRingBrush, 1.25 * scale));
        DrawEllipse(dc, cx, cy, 246 * scale, null, new Pen(SoftBlueBrush, 1.5 * scale) { DashStyle = new DashStyle(new[] { 4.0, 10.0 }, 0) });
        DrawEllipse(dc, cx, cy, 209 * scale, null, new Pen(MutedRingBrush, 0.9 * scale));
        DrawEllipse(dc, cx, cy, 168 * scale, null, new Pen(MutedRingBrush, 0.9 * scale));
        DrawEllipse(dc, cx, cy, 126 * scale, null, new Pen(MutedRingBrush, 0.8 * scale));
    }

    private void DrawOuterTotalRing(DrawingContext dc, double cx, double cy, double radius, double strokeWidth)
    {
        DrawTrack(dc, cx, cy, radius, strokeWidth, TrackBrush, 0.06, 0.035);
        var isActive = IsActive(TotalMetric);
        var isHovered = IsHovered(TotalMetric);
        var isDimmed = IsDimmed(TotalMetric);
        var opacity = isDimmed ? 0.24 : isHovered ? 0.9 : isActive ? 0.76 : 0.58;
        var arc = CreateArcGeometry(cx, cy, radius, -172, 178);

        dc.PushOpacity(isDimmed ? 0.08 : isHovered ? 0.2 : 0.14);
        dc.PushTransform(new TranslateTransform(0, 5 * (strokeWidth / 35.0)));
        dc.DrawGeometry(null, new Pen(ArcShadowBrush, strokeWidth + 7)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        }, arc);
        dc.Pop();
        dc.Pop();

        if (!isDimmed && isActive)
        {
            var glow = new Pen(GreyBrush, strokeWidth + (isHovered ? 24 : 16))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.PushOpacity(isHovered ? 0.26 : 0.16);
            dc.DrawGeometry(null, glow, arc);
            dc.Pop();
        }

        var pen = new Pen(TotalArcBrush, strokeWidth + (isHovered ? 7 : isActive ? 5 : 0))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(opacity);
        dc.DrawGeometry(null, pen, arc);
        dc.Pop();

        var edge = new Pen(ArcLowerEdgeBrush, 1.5 * (strokeWidth / 35.0))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(isDimmed ? 0.08 : isHovered ? 0.26 : isActive ? 0.2 : 0.15);
        dc.DrawGeometry(null, edge, CreateArcGeometry(cx, cy, radius + strokeWidth * 0.42, -164, 170));
        dc.Pop();

        var glint = new Pen(ArcSpecularBrush, 5.2 * (strokeWidth / 35.0))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(isDimmed ? 0.18 : isHovered ? 0.74 : isActive ? 0.64 : 0.54);
        dc.DrawGeometry(null, glint, CreateArcGeometry(cx, cy, radius - strokeWidth * 0.22, -146, -112));
        dc.DrawGeometry(null, glint, CreateArcGeometry(cx, cy, radius - strokeWidth * 0.18, 196, 216));
        dc.Pop();
    }

    private void DrawMetricArc(
        DrawingContext dc,
        double cx,
        double cy,
        double radius,
        double percent,
        string metric,
        Brush arcBrush,
        Brush solidBrush,
        string label,
        double scale,
        double pixelsPerDip)
    {
        var isActive = IsActive(metric);
        var dimmed = IsDimmed(metric);
        var stroke = (isActive ? 38 : 34) * scale;
        var startAngle = 0;
        var endAngle = Math.Clamp(percent, 0, 100) / 100.0 * 360.0;

        DrawSemanticTrack(dc, cx, cy, radius, 34 * scale, metric, solidBrush, isActive, dimmed);
        if (endAngle <= 0.1)
        {
            return;
        }

        dc.PushOpacity(dimmed ? 0.16 : isActive ? 0.34 : 0.22);
        dc.PushTransform(new TranslateTransform(0, 5 * scale));
        dc.DrawGeometry(null, new Pen(ArcShadowBrush, stroke + 7 * scale)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Round
        }, CreateArcGeometry(cx, cy, radius, startAngle + 1, endAngle));
        dc.Pop();
        dc.Pop();

        dc.PushOpacity(dimmed ? 0.32 : isActive ? 1.0 : 0.92);
        DrawArcGlow(dc, cx, cy, radius, startAngle, endAngle, solidBrush, (isActive ? 54 : 46) * scale, isActive ? 0.34 : 0.2);
        var pen = new Pen(arcBrush, stroke)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, CreateArcGeometry(cx, cy, radius, startAngle, endAngle));

        var lowerEdge = new Pen(ArcLowerEdgeBrush, 1.6 * scale)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Round
        };
        if (endAngle > startAngle + 6)
        {
            dc.PushOpacity(dimmed ? 0.14 : 0.3);
            dc.DrawGeometry(null, lowerEdge, CreateArcGeometry(cx, cy, radius + stroke * 0.38, startAngle + 2, endAngle - 1));
            dc.Pop();
        }

        var highlight = new Pen(ArcSpecularBrush, 4.2 * scale)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(dimmed ? 0.2 : isActive ? 0.82 : 0.62);
        if (endAngle > startAngle + 8)
        {
            dc.DrawGeometry(null, highlight, CreateArcGeometry(cx, cy, radius - stroke * 0.25, startAngle + 4, Math.Min(endAngle, startAngle + 34)));
        }
        if (endAngle > 105)
        {
            dc.DrawGeometry(null, highlight, CreateArcGeometry(cx, cy, radius - stroke * 0.22, Math.Max(startAngle + 86, endAngle - 45), Math.Max(startAngle + 96, endAngle - 13)));
        }
        dc.Pop();
        dc.Pop();

    }

    private void DrawTrack(
        DrawingContext dc,
        double cx,
        double cy,
        double radius,
        double strokeWidth,
        Brush trackBrush,
        double opacity,
        double depthOpacity)
    {
        var depthPen = new Pen(TrackDepthBrush, strokeWidth + 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(depthOpacity);
        dc.PushTransform(new TranslateTransform(0, 2.5 * (strokeWidth / 34.0)));
        dc.DrawEllipse(null, depthPen, new Point(cx, cy), radius, radius);
        dc.Pop();
        dc.Pop();

        var trackPen = new Pen(trackBrush, strokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(opacity);
        dc.DrawEllipse(null, trackPen, new Point(cx, cy), radius, radius);
        dc.Pop();
    }

    private void DrawSemanticTrack(
        DrawingContext dc,
        double cx,
        double cy,
        double radius,
        double strokeWidth,
        string metric,
        Brush trackBrush,
        bool isActive,
        bool dimmed)
    {
        var opacity = dimmed ? 0.025 : isActive ? 0.07 : 0.055;
        var depthOpacity = dimmed ? 0.012 : 0.02;

        DrawTrack(dc, cx, cy, radius, strokeWidth, trackBrush, opacity, depthOpacity);
    }

    private void DrawArcGlow(DrawingContext dc, double cx, double cy, double radius, double startAngle, double endAngle, Brush brush, double thickness, double opacity)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(opacity);
        dc.DrawGeometry(null, pen, CreateArcGeometry(cx, cy, radius, startAngle, endAngle));
        dc.Pop();
    }

    private void DrawCenterHub(DrawingContext dc, double cx, double cy, double scale, double pixelsPerDip)
    {
        var metric = HoveredMetricOrDefault ?? SelectedMetricOrDefault;
        var percentText = $"{MetricPercent(metric):0}%";
        var caption = MetricCaption(metric);

        var percent = Text(percentText, 40 * scale, HeavyTypeface, CenterTextBrush, pixelsPerDip);
        var label = Text(caption, 12.5 * scale, BoldTypeface, GreyBrush, pixelsPerDip);
        var gap = 7 * scale;
        var top = cy - (percent.Height + gap + label.Height) / 2.0 - 1 * scale;

        dc.DrawText(percent, new Point(cx - percent.Width / 2, top));
        dc.PushOpacity(0.82);
        dc.DrawText(label, new Point(cx - label.Width / 2, top + percent.Height + gap));
        dc.Pop();
    }

    private void DrawTicks(DrawingContext dc, double cx, double cy, double scale, double pixelsPerDip)
    {
        for (var i = 0; i < 72; i++)
        {
            var angle = i * 5.0;
            var major = i % 9 == 0;
            var inner = PointOnCircle(cx, cy, major ? 260 * scale : 263 * scale, angle);
            var outer = PointOnCircle(cx, cy, major ? 270 * scale : 267 * scale, angle);
            var pen = new Pen(OuterTickBrush, major ? 1.0 * scale : 0.55 * scale);
            dc.PushOpacity(major ? 0.32 : 0.18);
            dc.DrawLine(pen, inner, outer);
            dc.Pop();
        }
    }

    private string? HitTestMetric(Point point)
    {
        var size = Math.Max(1, Math.Min(ActualWidth, ActualHeight));
        var scale = size / 600.0;
        var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance >= 200 * scale && distance <= 250 * scale)
        {
            return TotalMetric;
        }

        if (IsRingHit(distance, 166 * scale, 34 * scale))
        {
            return IssuedMetric;
        }

        if (IsRingHit(distance, 123 * scale, 34 * scale))
        {
            return EffectiveMetric;
        }

        if (IsRingHit(distance, 81 * scale, 34 * scale))
        {
            return ExpiredMetric;
        }

        if (distance <= 86 * scale)
        {
            return SelectedMetricOrDefault;
        }

        return null;
    }

    private static bool IsRingHit(double distance, double radius, double stroke)
    {
        return distance >= radius - stroke * 0.8 && distance <= radius + stroke * 0.8;
    }

    private bool IsActive(string metric)
    {
        var hovered = HoveredMetricOrDefault;
        if (!string.IsNullOrWhiteSpace(hovered))
        {
            return string.Equals(hovered, metric, StringComparison.Ordinal);
        }

        return string.Equals(SelectedMetricOrDefault, metric, StringComparison.Ordinal);
    }

    private bool IsDimmed(string metric)
    {
        var hovered = HoveredMetricOrDefault;
        if (!string.IsNullOrWhiteSpace(hovered))
        {
            return !string.Equals(hovered, metric, StringComparison.Ordinal);
        }

        var selected = SelectedMetricOrDefault;
        return !string.Equals(selected, TotalMetric, StringComparison.Ordinal)
            && !string.Equals(selected, metric, StringComparison.Ordinal);
    }

    private bool IsHovered(string metric)
    {
        return string.Equals(HoveredMetricOrDefault, metric, StringComparison.Ordinal);
    }

    private string? HoveredMetricOrDefault => string.IsNullOrWhiteSpace(HoveredMetric) ? null : HoveredMetric;

    private string SelectedMetricOrDefault => string.IsNullOrWhiteSpace(SelectedMetric) ? TotalMetric : SelectedMetric;

    private double MetricPercent(string metric)
    {
        return metric switch
        {
            IssuedMetric => IssuedPercent,
            EffectiveMetric => EffectivePercent,
            ExpiredMetric => ExpiredPercent,
            _ => 100
        };
    }

    private static string MetricCaption(string metric)
    {
        return metric switch
        {
            IssuedMetric => "PH\u00c1T H\u00c0NH",
            EffectiveMetric => "C\u00d2N HI\u1ec6U L\u1ef0C",
            ExpiredMetric => "H\u1ebeT HI\u1ec6U L\u1ef0C",
            _ => "T\u1ed4NG S\u1ed0 V\u0102N B\u1ea2N"
        };
    }

    private static void DrawEllipse(DrawingContext dc, double cx, double cy, double radius, Brush? fill, Pen? pen)
    {
        dc.DrawEllipse(fill, pen, new Point(cx, cy), radius, radius);
    }

    private static Geometry CreateArcGeometry(double cx, double cy, double radius, double startAngle, double endAngle)
    {
        if (Math.Abs(endAngle - startAngle) >= 359.9)
        {
            endAngle = startAngle + 359.9;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var start = PointOnCircle(cx, cy, radius, startAngle);
            var end = PointOnCircle(cx, cy, radius, endAngle);
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0, Math.Abs(endAngle - startAngle) > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(double cx, double cy, double radius, double angleDegrees)
    {
        var angle = (angleDegrees - 90) * Math.PI / 180.0;
        return new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
    }

    private static FormattedText Text(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            Math.Max(1, size),
            brush,
            pixelsPerDip);
    }

    private static Brush CreateAmbientWashBrush()
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.42, 0.34),
            GradientOrigin = new Point(0.28, 0.18),
            RadiusX = 0.9,
            RadiusY = 0.92,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(70, 125, 211, 252), 0),
                new GradientStop(Color.FromArgb(34, 59, 130, 246), 0.52),
                new GradientStop(Color.FromArgb(0, 14, 165, 233), 1)
            }
        };
    }

    private static Brush CreateGradient(string c1, string c2, string c3, string c4)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0.18, 0.02),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString(c2), 0.32),
                new GradientStop((Color)ColorConverter.ConvertFromString(c3), 0.68),
                new GradientStop((Color)ColorConverter.ConvertFromString(c4), 1)
            }
        };
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
