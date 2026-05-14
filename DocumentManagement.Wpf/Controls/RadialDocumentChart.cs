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

    private static readonly Brush SurfaceBrush = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Brush MutedRingBrush = Freeze(new SolidColorBrush(Color.FromArgb(58, 147, 197, 253)));
    private static readonly Brush OuterTickBrush = Freeze(new SolidColorBrush(Color.FromArgb(112, 96, 165, 250)));
    private static readonly Brush SoftBlueBrush = Freeze(new SolidColorBrush(Color.FromArgb(118, 191, 219, 254)));
    private static readonly Brush CenterTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(7, 26, 100)));
    private static readonly Brush GreyBrush = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));
    private static readonly Brush BlueBrush = Freeze(new SolidColorBrush(Color.FromRgb(37, 99, 235)));
    private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(16, 185, 129)));
    private static readonly Brush RedBrush = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
    private static readonly Brush CyanWashBrush = Freeze(new RadialGradientBrush(Color.FromArgb(42, 34, 211, 238), Color.FromArgb(0, 34, 211, 238)));
    private static readonly Brush TotalArcBrush = Freeze(CreateGradient("#DBEAFE", "#A5B4FC", "#60A5FA"));
    private static readonly Brush IssuedArcBrush = Freeze(CreateGradient("#60A5FA", "#2563EB", "#22D3EE"));
    private static readonly Brush EffectiveArcBrush = Freeze(CreateGradient("#6EE7B7", "#10B981", "#22C55E"));
    private static readonly Brush ExpiredArcBrush = Freeze(CreateGradient("#FB7185", "#EF4444", "#F97316"));

    private string? _hoveredMetric;

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
        var metric = HitTestMetric(e.GetPosition(this));
        if (metric == _hoveredMetric)
        {
            return;
        }

        _hoveredMetric = metric;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredMetric == null)
        {
            return;
        }

        _hoveredMetric = null;
        InvalidateVisual();
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
        DrawStaticPercentMarkers(dc, cx, cy, scale, dpi);

        DrawOuterTotalRing(dc, cx, cy, 224 * scale, 35 * scale);
        DrawMetricArc(dc, cx, cy, 166 * scale, IssuedPercent, IssuedMetric, IssuedArcBrush, BlueBrush, "Phát hành", scale, dpi);
        DrawMetricArc(dc, cx, cy, 123 * scale, EffectivePercent, EffectiveMetric, EffectiveArcBrush, GreenBrush, "Còn hiệu lực", scale, dpi);
        DrawMetricArc(dc, cx, cy, 81 * scale, ExpiredPercent, ExpiredMetric, ExpiredArcBrush, RedBrush, "Hết hiệu lực", scale, dpi);
        DrawTotalBadge(dc, cx, cy, scale, dpi);
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
        DrawEllipse(dc, cx, cy, 269 * scale, null, new Pen(MutedRingBrush, 1.2 * scale));
        DrawEllipse(dc, cx, cy, 246 * scale, null, new Pen(SoftBlueBrush, 2.3 * scale) { DashStyle = new DashStyle(new[] { 5.0, 8.0 }, 0) });
        DrawEllipse(dc, cx, cy, 209 * scale, null, new Pen(MutedRingBrush, 1.1 * scale));
        DrawEllipse(dc, cx, cy, 168 * scale, null, new Pen(MutedRingBrush, 0.9 * scale));
        DrawEllipse(dc, cx, cy, 126 * scale, null, new Pen(MutedRingBrush, 0.8 * scale));
    }

    private void DrawOuterTotalRing(DrawingContext dc, double cx, double cy, double radius, double strokeWidth)
    {
        DrawTrack(dc, cx, cy, radius, strokeWidth, 0.42);
        var isActive = IsActive(TotalMetric);
        var opacity = IsDimmed(TotalMetric) ? 0.32 : 0.86;
        var pen = new Pen(TotalArcBrush, strokeWidth + (isActive ? 4 : 0))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(opacity);
        dc.DrawGeometry(null, pen, CreateArcGeometry(cx, cy, radius, -172, 178));
        dc.Pop();

        var glint = new Pen(Freeze(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255))), 4.8 * (strokeWidth / 35.0))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, glint, CreateArcGeometry(cx, cy, radius, 196, 214));
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

        DrawTrack(dc, cx, cy, radius, 34 * scale, dimmed ? 0.12 : 0.28);

        dc.PushOpacity(dimmed ? 0.26 : isActive ? 1.0 : 0.88);
        DrawArcGlow(dc, cx, cy, radius, startAngle, endAngle, solidBrush, (isActive ? 50 : 43) * scale, isActive ? 0.28 : 0.16);
        var pen = new Pen(arcBrush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, CreateArcGeometry(cx, cy, radius, startAngle, endAngle));

        var highlight = new Pen(Freeze(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255))), 4 * scale)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, highlight, CreateArcGeometry(cx, cy, radius, startAngle + 2, Math.Min(endAngle, startAngle + 25)));
        dc.Pop();

        var node = PointOnCircle(cx, cy, radius, endAngle);
        DrawNode(dc, node, solidBrush, scale, isActive, dimmed);
        DrawNodeLabel(dc, node, percent, solidBrush, scale, pixelsPerDip, isActive, dimmed);
    }

    private void DrawTrack(DrawingContext dc, double cx, double cy, double radius, double strokeWidth, double opacity)
    {
        var trackPen = new Pen(Freeze(new SolidColorBrush(Color.FromArgb(84, 174, 194, 224))), strokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(opacity);
        dc.DrawEllipse(null, trackPen, new Point(cx, cy), radius, radius);
        dc.Pop();
    }

    private void DrawArcGlow(DrawingContext dc, double cx, double cy, double radius, double startAngle, double endAngle, Brush brush, double thickness, double opacity)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(opacity);
        dc.DrawGeometry(null, pen, CreateArcGeometry(cx, cy, radius, startAngle, endAngle));
        dc.Pop();
    }

    private void DrawNode(DrawingContext dc, Point node, Brush brush, double scale, bool active, bool dimmed)
    {
        var outer = (active ? 34 : 29) * scale;
        dc.PushOpacity(dimmed ? 0.28 : 1.0);
        dc.DrawEllipse(Freeze(new SolidColorBrush(Color.FromArgb(active ? (byte)88 : (byte)48, 37, 99, 235))), null, node, outer * 0.88, outer * 0.88);
        dc.DrawEllipse(SurfaceBrush, new Pen(brush, active ? 5 * scale : 4 * scale), node, outer * 0.42, outer * 0.42);
        dc.DrawEllipse(brush, null, new Point(node.X + outer * 0.08, node.Y - outer * 0.08), outer * 0.16, outer * 0.16);
        dc.Pop();
    }

    private void DrawNodeLabel(DrawingContext dc, Point node, double percent, Brush brush, double scale, double pixelsPerDip, bool active, bool dimmed)
    {
        var text = $"{percent:0}%";
        var formatted = Text(text, active ? 22 * scale : 20 * scale, active ? HeavyTypeface : BoldTypeface, brush, pixelsPerDip);
        var offsetX = node.X > ActualWidth / 2 ? 18 * scale : -formatted.Width - 18 * scale;
        var offsetY = node.Y > ActualHeight / 2 ? 5 * scale : -formatted.Height - 2 * scale;

        dc.PushOpacity(dimmed ? 0.32 : active ? 1.0 : 0.86);
        dc.DrawText(formatted, new Point(node.X + offsetX, node.Y + offsetY));
        dc.Pop();
    }

    private void DrawTotalBadge(DrawingContext dc, double cx, double cy, double scale, double pixelsPerDip)
    {
        var p = PointOnCircle(cx, cy, 247 * scale, 0);
        var active = IsActive(TotalMetric);
        dc.DrawEllipse(Freeze(new SolidColorBrush(Color.FromArgb(78, 37, 99, 235))), null, p, (active ? 53 : 48) * scale, (active ? 53 : 48) * scale);
        dc.DrawEllipse(SurfaceBrush, new Pen(SoftBlueBrush, active ? 4.5 * scale : 3.5 * scale), p, 43 * scale, 43 * scale);

        var percent = Text("100%", 23 * scale, HeavyTypeface, BlueBrush, pixelsPerDip);
        var title = Text("TỔNG SỐ", 11.5 * scale, BoldTypeface, BlueBrush, pixelsPerDip);
        dc.DrawText(percent, new Point(p.X - percent.Width / 2, p.Y - 22 * scale));
        dc.DrawText(title, new Point(p.X - title.Width / 2, p.Y + 9 * scale));
    }

    private void DrawCenterHub(DrawingContext dc, double cx, double cy, double scale, double pixelsPerDip)
    {
        var metric = SelectedMetricOrDefault;
        var value = MetricValue(metric);
        var caption = MetricCaption(metric);
        var percent = metric == TotalMetric ? "100%" : $"{MetricPercent(metric):0}%";

        dc.DrawEllipse(Freeze(new SolidColorBrush(Color.FromArgb(72, 37, 99, 235))), null, new Point(cx, cy + 7 * scale), 92 * scale, 92 * scale);
        dc.DrawEllipse(SurfaceBrush, new Pen(SoftBlueBrush, 2.5 * scale), new Point(cx, cy), 80 * scale, 80 * scale);

        var number = Text(value.ToString("N0", CultureInfo.CurrentCulture), 56 * scale, HeavyTypeface, CenterTextBrush, pixelsPerDip);
        var unit = Text(metric == TotalMetric ? "VB" : percent, metric == TotalMetric ? 31 * scale : 25 * scale, BoldTypeface, CenterTextBrush, pixelsPerDip);
        var detail = Text(caption, 12 * scale, BoldTypeface, GreyBrush, pixelsPerDip);

        dc.DrawText(number, new Point(cx - number.Width / 2, cy - 51 * scale));
        dc.DrawText(unit, new Point(cx - unit.Width / 2, cy + 16 * scale));
        dc.DrawText(detail, new Point(cx - detail.Width / 2, cy + 52 * scale));
    }

    private void DrawTicks(DrawingContext dc, double cx, double cy, double scale, double pixelsPerDip)
    {
        for (var i = 0; i < 96; i++)
        {
            var angle = i * 3.75;
            var major = i % 8 == 0;
            var inner = PointOnCircle(cx, cy, major ? 257 * scale : 262 * scale, angle);
            var outer = PointOnCircle(cx, cy, major ? 271 * scale : 267 * scale, angle);
            var pen = new Pen(OuterTickBrush, major ? 1.4 * scale : 0.75 * scale);
            dc.PushOpacity(major ? 0.66 : 0.34);
            dc.DrawLine(pen, inner, outer);
            dc.Pop();
        }

        for (var i = 0; i < 8; i++)
        {
            var dot = PointOnCircle(cx, cy, 268 * scale, i * 45 + 12);
            dc.DrawEllipse(BlueBrush, null, dot, 3.2 * scale, 3.2 * scale);
        }
    }

    private void DrawStaticPercentMarkers(DrawingContext dc, double cx, double cy, double scale, double pixelsPerDip)
    {
        DrawMarker(dc, "0%", cx, cy, 281 * scale, 0, -11 * scale, -19 * scale, scale, pixelsPerDip);
        DrawMarker(dc, "25%", cx, cy, 281 * scale, 90, 8 * scale, -7 * scale, scale, pixelsPerDip);
        DrawMarker(dc, "50%", cx, cy, 281 * scale, 180, -17 * scale, 14 * scale, scale, pixelsPerDip);
        DrawMarker(dc, "75%", cx, cy, 281 * scale, 270, -45 * scale, -7 * scale, scale, pixelsPerDip);
    }

    private void DrawMarker(DrawingContext dc, string label, double cx, double cy, double radius, double angle, double dx, double dy, double scale, double pixelsPerDip)
    {
        var p = PointOnCircle(cx, cy, radius, angle);
        var text = Text(label, 14 * scale, BoldTypeface, BlueBrush, pixelsPerDip);
        dc.PushOpacity(0.78);
        dc.DrawText(text, new Point(p.X + dx, p.Y + dy));
        dc.Pop();
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
        return string.Equals(_hoveredMetric, metric, StringComparison.Ordinal)
            || string.Equals(SelectedMetricOrDefault, metric, StringComparison.Ordinal);
    }

    private bool IsDimmed(string metric)
    {
        var active = _hoveredMetric ?? SelectedMetricOrDefault;
        return !string.IsNullOrWhiteSpace(active)
            && !string.Equals(active, metric, StringComparison.Ordinal);
    }

    private string SelectedMetricOrDefault => string.IsNullOrWhiteSpace(SelectedMetric) ? TotalMetric : SelectedMetric;

    private int MetricValue(string metric)
    {
        return metric switch
        {
            IssuedMetric => IssuedDocuments,
            EffectiveMetric => EffectiveDocuments,
            ExpiredMetric => ExpiredDocuments,
            _ => TotalDocuments
        };
    }

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
            IssuedMetric => "PHÁT HÀNH",
            EffectiveMetric => "CÒN HIỆU LỰC",
            ExpiredMetric => "HẾT HIỆU LỰC",
            _ => "TỔNG SỐ"
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

    private static Brush CreateGradient(string c1, string c2, string c3)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString(c2), 0.55),
                new GradientStop((Color)ColorConverter.ConvertFromString(c3), 1)
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
