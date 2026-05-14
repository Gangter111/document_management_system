using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Contracts.Dashboard;

namespace DocumentManagement.Wpf.Controls;

public class MonthlyIssuedTimelineChart : Canvas
{
    private static readonly Brush NavyTextBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromRgb(8, 34, 76)));
    private static readonly Brush AxisTextBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromRgb(44, 83, 128)));
    private static readonly Brush MutedTextBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromRgb(82, 111, 145)));
    private static readonly Brush AxisBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromArgb(116, 134, 169, 205)));
    private static readonly Brush GridBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromArgb(72, 203, 221, 236)));
    private static readonly Brush PlotBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient("#FFFFFF", "#F6FBFF", new Point(0, 0), new Point(1, 1)));
    private static readonly Brush PlotGlowBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.RadialBrush(Color.FromArgb(24, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));
    private static readonly Brush FrontBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.ThreeStopGradient("#65ECF4", "#1AA7EF", "#0869EA", new Point(0, 0), new Point(0, 1)));
    private static readonly Brush FrontActiveBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.ThreeStopGradient("#7EF5FB", "#159EF7", "#075FE3", new Point(0, 0), new Point(0, 1)));
    private static readonly Brush SideBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient("#38D2E2", "#1276C9", new Point(0, 0), new Point(1, 1)));
    private static readonly Brush TopBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient("#C5FBFC", "#42D8E8", new Point(0, 0), new Point(1, 1)));
    private static readonly Brush PlatformBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient("#FFFFFF", "#E0ECF8", new Point(0, 0), new Point(1, 1)));
    private static readonly Brush HighlightBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient(Color.FromArgb(105, 255, 255, 255), Color.FromArgb(16, 255, 255, 255), new Point(0, 0), new Point(1, 0)));
    private static readonly Brush EdgeLightBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromArgb(95, 205, 250, 255)));
    private static readonly Brush ReflectionBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient(Color.FromArgb(42, 10, 120, 244), Color.FromArgb(0, 10, 120, 244), new Point(0, 0), new Point(0, 1)));
    private static readonly Brush ZeroBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient("#58E8FF", "#0B7AF4", new Point(0, 0), new Point(1, 1)));
    private static readonly Brush ZeroGlowBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.RadialBrush(Color.FromArgb(58, 12, 112, 230), Color.FromArgb(0, 12, 112, 230)));
    private static readonly Brush TooltipBrush = DashboardChartDrawing.Freeze(DashboardChartDrawing.TwoStopGradient("#FFFFFF", "#F7FBFF", new Point(0, 0), new Point(1, 1)));
    private static readonly Brush TooltipBorderBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromRgb(209, 227, 243)));
    private static readonly Brush ShadowBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromArgb(44, 35, 111, 205)));
    private static readonly Brush BarBorderBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromArgb(140, 180, 238, 255)));
    private static readonly Brush PlatformBorderBrush = DashboardChartDrawing.Freeze(new SolidColorBrush(Color.FromRgb(200, 216, 232)));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(MonthlyIssuedTimelineChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    private readonly Stopwatch _animationClock = new();
    private int _hoveredIndex = -1;
    private Rect[] _hitRects = [];
    private Point[] _tooltipAnchors = [];
    private IReadOnlyList<DashboardChartItemDto> _items = [];
    private INotifyCollectionChanged? _observableItems;
    private bool _isAnimating;

    public MonthlyIssuedTimelineChart()
    {
        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Cursor = Cursors.Arrow;
        Loaded += (_, _) => StartIntroAnimation();
        SizeChanged += (_, _) => StartIntroAnimation();
        Unloaded += (_, _) =>
        {
            StopIntroAnimation();
            DetachCollectionChanged();
        };
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var point = e.GetPosition(this);
        var hit = -1;
        for (var i = 0; i < _hitRects.Length; i++)
        {
            if (_hitRects[i].Contains(point))
            {
                hit = i;
                break;
            }
        }

        if (hit == _hoveredIndex)
        {
            return;
        }

        _hoveredIndex = hit;
        Cursor = hit >= 0 ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex < 0)
        {
            return;
        }

        _hoveredIndex = -1;
        Cursor = Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 120 || height < 120)
        {
            return;
        }

        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var scale = Math.Min(width / 560.0, height / 300.0);
        var left = 50 * scale;
        var right = 18 * scale;
        var top = 30 * scale;
        var bottom = 44 * scale;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var baselineY = top + plotHeight;

        DrawPlotSurface(dc, scale);
        DrawYAxisTitle(dc, scale, pixelsPerDip);

        if (_items.Count == 0)
        {
            _hitRects = [];
            _tooltipAnchors = [];
            DrawGrid(dc, left, top, plotWidth, plotHeight, 8, scale, pixelsPerDip);
            DrawEmptyState(dc, left, top, plotWidth, plotHeight, scale, pixelsPerDip);
            return;
        }

        var maxValue = GetNiceMax(Math.Max(8, _items.Max(item => item.Value)));
        _hitRects = new Rect[_items.Count];
        _tooltipAnchors = new Point[_items.Count];

        DrawGrid(dc, left, top, plotWidth, plotHeight, maxValue, scale, pixelsPerDip);
        DrawTimeline(dc, left, plotWidth, plotHeight, baselineY, maxValue, scale, pixelsPerDip);

        if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
        {
            DrawTooltip(dc, _items[_hoveredIndex], _tooltipAnchors[_hoveredIndex], scale, pixelsPerDip);
        }
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MonthlyIssuedTimelineChart chart)
        {
            return;
        }

        chart.DetachCollectionChanged();
        chart.AttachCollectionChanged(e.NewValue);
        chart.RebuildItems();
    }

    private void AttachCollectionChanged(object? source)
    {
        if (source is INotifyCollectionChanged observable)
        {
            _observableItems = observable;
            _observableItems.CollectionChanged += ItemsSource_CollectionChanged;
        }
    }

    private void DetachCollectionChanged()
    {
        if (_observableItems == null)
        {
            return;
        }

        _observableItems.CollectionChanged -= ItemsSource_CollectionChanged;
        _observableItems = null;
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildItems();
    }

    private void RebuildItems()
    {
        _items = ItemsSource?
            .OfType<DashboardChartItemDto>()
            .Select(item => new DashboardChartItemDto
            {
                Name = string.IsNullOrWhiteSpace(item.Name) ? "-" : item.Name.Trim(),
                Value = Math.Max(0, item.Value)
            })
            .ToList()
            ?? [];

        _hoveredIndex = -1;
        _hitRects = [];
        _tooltipAnchors = [];
        StartIntroAnimation();
        InvalidateVisual();
    }

    private void StartIntroAnimation()
    {
        if (!IsLoaded)
        {
            return;
        }

        StopIntroAnimation();
        _animationClock.Restart();
        _isAnimating = true;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        InvalidateVisual();
    }

    private void StopIntroAnimation()
    {
        if (!_isAnimating)
        {
            return;
        }

        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        _isAnimating = false;
        _animationClock.Stop();
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (GetAnimationProgress() >= 1)
        {
            StopIntroAnimation();
        }

        InvalidateVisual();
    }

    private double GetAnimationProgress()
    {
        if (!_isAnimating)
        {
            return 1;
        }

        var t = Math.Clamp(_animationClock.Elapsed.TotalMilliseconds / 520.0, 0, 1);
        return 1 - Math.Pow(1 - t, 3);
    }

    private void DrawPlotSurface(DrawingContext dc, double scale)
    {
        var rect = new Rect(0.5 * scale, 0.5 * scale, ActualWidth - scale, ActualHeight - scale);
        dc.DrawRoundedRectangle(PlotBrush, new Pen(TooltipBorderBrush, 0.6 * scale), rect, 13 * scale, 13 * scale);
        dc.DrawRoundedRectangle(PlotGlowBrush, null, rect, 13 * scale, 13 * scale);
    }

    private void DrawYAxisTitle(DrawingContext dc, double scale, double pixelsPerDip)
    {
        var title = Text("Số văn bản", 10.5 * scale, DashboardChartDrawing.SemiBoldTypeface, AxisTextBrush, pixelsPerDip);
        dc.DrawText(title, new Point(18 * scale, 16 * scale));
    }

    private void DrawGrid(DrawingContext dc, double left, double top, double plotWidth, double plotHeight, int maxValue, double scale, double pixelsPerDip)
    {
        var steps = maxValue <= 8 ? maxValue : Math.Min(6, maxValue);
        var gridPen = new Pen(GridBrush, 0.55 * scale)
        {
            DashStyle = new DashStyle([4.8, 5.4], 0)
        };
        var axisPen = new Pen(AxisBrush, 0.75 * scale);

        for (var i = 0; i <= steps; i++)
        {
            var value = (int)Math.Round(maxValue * i / (double)steps);
            var y = top + plotHeight - plotHeight * i / steps;
            var label = Text(value.ToString(CultureInfo.CurrentCulture), 9.2 * scale, DashboardChartDrawing.MediumTypeface, AxisTextBrush, pixelsPerDip);
            dc.DrawText(label, new Point(left - label.Width - 12 * scale, y - label.Height / 2));

            dc.PushOpacity(i == 0 ? 0.78 : 0.55);
            dc.DrawLine(i == 0 ? axisPen : gridPen, new Point(left, y), new Point(left + plotWidth, y));
            dc.Pop();
        }

        dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + plotHeight));
    }

    private void DrawTimeline(DrawingContext dc, double left, double plotWidth, double plotHeight, double baselineY, int maxValue, double scale, double pixelsPerDip)
    {
        var progress = GetAnimationProgress();
        var section = plotWidth / _items.Count;
        var barWidth = Math.Min(35 * scale, Math.Max(14 * scale, section * 0.52));
        var depthX = Math.Min(9 * scale, barWidth * 0.26);
        var depthY = -Math.Min(7 * scale, barWidth * 0.2);

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var centerX = left + section * i + section / 2;
            var active = i == _hoveredIndex;

            if (item.Value <= 0)
            {
                DrawZeroMarker(dc, centerX, baselineY, scale, pixelsPerDip);
                _hitRects[i] = new Rect(centerX - section / 2, baselineY - 26 * scale, section, 38 * scale);
                _tooltipAnchors[i] = new Point(centerX, baselineY - 18 * scale);
            }
            else
            {
                var targetHeight = Math.Max(12 * scale, plotHeight * item.Value / maxValue * 0.9);
                var barHeight = targetHeight * progress;
                var x = centerX - barWidth / 2;
                var y = baselineY - barHeight;

                DrawBarReflection(dc, x, baselineY, barWidth, barHeight, scale);
                DrawPlatform(dc, x, baselineY, barWidth, depthX, scale, active);
                DrawRoundedDepthBar(dc, x, y, barWidth, barHeight, depthX, depthY, scale, active);
                DrawValueLabel(dc, item.Value, centerX + depthX / 2, y - 18 * scale, scale, pixelsPerDip, active);

                _hitRects[i] = new Rect(x - 5 * scale, y + depthY - 5 * scale, barWidth + depthX + 10 * scale, barHeight - depthY + 12 * scale);
                _tooltipAnchors[i] = new Point(centerX + depthX / 2, y - 4 * scale);
            }

            if (ShouldDrawAxisLabel(i))
            {
                DrawAxisLabel(dc, item.Name, centerX, baselineY + 15 * scale, section, scale, pixelsPerDip);
            }
        }
    }

    private bool ShouldDrawAxisLabel(int index)
    {
        if (_items.Count <= 6)
        {
            return true;
        }

        return index == 0 || index == _items.Count - 1 || index == _items.Count / 2 || _items[index].Value > 0;
    }

    private void DrawRoundedDepthBar(DrawingContext dc, double x, double y, double width, double height, double depthX, double depthY, double scale, bool active)
    {
        if (height <= 0.5)
        {
            return;
        }

        var corner = Math.Min(8 * scale, width * 0.3);

        dc.PushOpacity(active ? 1.0 : 0.96);

        dc.PushOpacity(active ? 0.56 : 0.38);
        dc.DrawRoundedRectangle(ShadowBrush, null, new Rect(x + 7 * scale, y + 8 * scale, width, height), corner, corner);
        dc.Pop();

        var side = new StreamGeometry();
        using (var ctx = side.Open())
        {
            ctx.BeginFigure(new Point(x + width - corner * 0.8, y + corner * 0.7), true, true);
            ctx.QuadraticBezierTo(new Point(x + width, y), new Point(x + width + depthX, y + depthY + corner * 0.8), true, false);
            ctx.LineTo(new Point(x + width + depthX, y + height + depthY - corner * 0.65), true, false);
            ctx.QuadraticBezierTo(new Point(x + width + depthX, y + height + depthY), new Point(x + width, y + height), true, false);
            ctx.LineTo(new Point(x + width - corner * 0.8, y + height), true, false);
        }
        side.Freeze();
        dc.DrawGeometry(SideBrush, null, side);

        dc.DrawRoundedRectangle(active ? FrontActiveBrush : FrontBrush, new Pen(BarBorderBrush, 0.65 * scale), new Rect(x, y, width, height), corner, corner);

        var top = new StreamGeometry();
        using (var ctx = top.Open())
        {
            ctx.BeginFigure(new Point(x + corner, y), true, true);
            ctx.LineTo(new Point(x + width - corner, y), true, false);
            ctx.QuadraticBezierTo(new Point(x + width, y), new Point(x + width + depthX, y + depthY + corner * 0.8), true, false);
            ctx.LineTo(new Point(x + depthX + corner, y + depthY + corner * 0.8), true, false);
            ctx.QuadraticBezierTo(new Point(x, y), new Point(x + corner, y), true, false);
        }
        top.Freeze();
        dc.DrawGeometry(TopBrush, new Pen(BarBorderBrush, 0.55 * scale), top);

        var highlightWidth = width * 0.24;
        var highlightHeight = Math.Max(0, height - 13 * scale);
        if (highlightHeight > 2)
        {
            dc.PushOpacity(active ? 0.76 : 0.58);
            dc.DrawRoundedRectangle(HighlightBrush, null, new Rect(x + 6 * scale, y + 8 * scale, highlightWidth, highlightHeight), corner * 0.7, corner * 0.7);
            dc.Pop();
        }

        if (height > 20 * scale)
        {
            dc.PushOpacity(active ? 0.68 : 0.46);
            dc.DrawLine(new Pen(EdgeLightBrush, 0.6 * scale), new Point(x + width - 2 * scale, y + 7 * scale), new Point(x + width - 2 * scale, y + height - 3 * scale));
            dc.Pop();
        }

        dc.Pop();
    }

    private void DrawZeroMarker(DrawingContext dc, double centerX, double baselineY, double scale, double pixelsPerDip)
    {
        DrawValueLabel(dc, 0, centerX, baselineY - 22 * scale, scale, pixelsPerDip, false);

        dc.DrawEllipse(ZeroGlowBrush, null, new Point(centerX, baselineY + 4 * scale), 10 * scale, 6 * scale);
        dc.DrawEllipse(Brushes.White, new Pen(ZeroBrush, 1.5 * scale), new Point(centerX, baselineY), 4.5 * scale, 4.5 * scale);
        dc.DrawEllipse(ZeroBrush, null, new Point(centerX, baselineY), 2.1 * scale, 2.1 * scale);
    }

    private void DrawPlatform(DrawingContext dc, double x, double baselineY, double width, double depthX, double scale, bool active)
    {
        var platformRect = new Rect(x - 8 * scale, baselineY - 2 * scale, width + depthX + 16 * scale, 8.5 * scale);

        dc.PushOpacity(active ? 0.62 : 0.42);
        dc.DrawEllipse(ShadowBrush, null, new Point(platformRect.X + platformRect.Width / 2, baselineY + 6 * scale), platformRect.Width * 0.44, 4.5 * scale);
        dc.Pop();

        dc.DrawRoundedRectangle(PlatformBrush, new Pen(PlatformBorderBrush, 0.5 * scale), platformRect, 4 * scale, 4 * scale);
    }

    private void DrawBarReflection(DrawingContext dc, double x, double baselineY, double width, double height, double scale)
    {
        var reflectionHeight = Math.Min(24 * scale, height * 0.22);
        if (reflectionHeight <= 1)
        {
            return;
        }

        dc.PushOpacity(0.28);
        dc.DrawRoundedRectangle(ReflectionBrush, null, new Rect(x, baselineY + 7 * scale, width, reflectionHeight), 6 * scale, 6 * scale);
        dc.Pop();
    }

    private static void DrawValueLabel(DrawingContext dc, int value, double centerX, double y, double scale, double pixelsPerDip, bool active)
    {
        var text = Text(value.ToString(CultureInfo.CurrentCulture), active ? 11.2 * scale : 10.2 * scale, DashboardChartDrawing.BoldTypeface, NavyTextBrush, pixelsPerDip);
        dc.DrawText(text, new Point(centerX - text.Width / 2, y));
    }

    private void DrawAxisLabel(DrawingContext dc, string label, double centerX, double y, double section, double scale, double pixelsPerDip)
    {
        var text = Text(label, 9.2 * scale, DashboardChartDrawing.MediumTypeface, MutedTextBrush, pixelsPerDip);
        text.MaxTextWidth = Math.Max(26 * scale, section - 4 * scale);
        text.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(text, new Point(centerX - text.Width / 2, y));
    }

    private void DrawTooltip(DrawingContext dc, DashboardChartItemDto item, Point anchor, double scale, double pixelsPerDip)
    {
        var title = Text(item.Name, 10.8 * scale, DashboardChartDrawing.BoldTypeface, NavyTextBrush, pixelsPerDip);
        var detail = Text($"Văn bản ban hành: {item.Value:N0}", 9.4 * scale, DashboardChartDrawing.SemiBoldTypeface, NavyTextBrush, pixelsPerDip);
        var width = Math.Max(title.Width, detail.Width + 22 * scale) + 24 * scale;
        var height = title.Height + detail.Height + 20 * scale;
        var x = Math.Clamp(anchor.X - width * 0.58, 8 * scale, ActualWidth - width - 8 * scale);
        var y = Math.Max(6 * scale, anchor.Y - height - 18 * scale);
        var rect = new Rect(x, y, width, height);

        dc.PushOpacity(0.24);
        dc.DrawRoundedRectangle(ShadowBrush, null, new Rect(rect.X + 3 * scale, rect.Y + 5 * scale, rect.Width, rect.Height), 9 * scale, 9 * scale);
        dc.Pop();

        dc.DrawRoundedRectangle(TooltipBrush, new Pen(TooltipBorderBrush, 0.8 * scale), rect, 8 * scale, 8 * scale);
        dc.DrawText(title, new Point(rect.X + (rect.Width - title.Width) / 2, rect.Y + 8 * scale));
        dc.DrawEllipse(ZeroBrush, null, new Point(rect.X + 14 * scale, rect.Y + title.Height + 18 * scale), 4 * scale, 4 * scale);
        dc.DrawText(detail, new Point(rect.X + 24 * scale, rect.Y + title.Height + 12 * scale));

        var arrow = new StreamGeometry();
        using (var ctx = arrow.Open())
        {
            var arrowX = Math.Clamp(anchor.X, rect.Left + 18 * scale, rect.Right - 18 * scale);
            ctx.BeginFigure(new Point(arrowX - 7 * scale, rect.Bottom - 1), true, true);
            ctx.LineTo(new Point(arrowX + 7 * scale, rect.Bottom - 1), true, false);
            ctx.LineTo(new Point(arrowX, rect.Bottom + 9 * scale), true, false);
        }
        arrow.Freeze();
        dc.DrawGeometry(TooltipBrush, new Pen(TooltipBorderBrush, 0.8 * scale), arrow);
    }

    private void DrawEmptyState(DrawingContext dc, double left, double top, double plotWidth, double plotHeight, double scale, double pixelsPerDip)
    {
        var message = Text("Chưa có dữ liệu ban hành theo tháng", 12 * scale, DashboardChartDrawing.SemiBoldTypeface, MutedTextBrush, pixelsPerDip);
        dc.DrawText(message, new Point(left + (plotWidth - message.Width) / 2, top + (plotHeight - message.Height) / 2));
    }

    private static int GetNiceMax(int value)
    {
        if (value <= 8)
        {
            return 8;
        }

        var step = value <= 20 ? 5 : 10;
        return (int)Math.Ceiling(value / (double)step) * step;
    }

    private static FormattedText Text(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip)
    {
        return DashboardChartDrawing.Text(text, size, typeface, brush, pixelsPerDip);
    }
}
