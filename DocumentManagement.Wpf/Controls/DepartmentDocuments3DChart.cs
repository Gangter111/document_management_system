using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Contracts.Dashboard;

namespace DocumentManagement.Wpf.Controls;

public class DepartmentDocuments3DChart : Canvas
{
    private static readonly Typeface RegularTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface MediumTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private static readonly Typeface SemiBoldTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(70, 86, 104)));
    private static readonly Brush MutedTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(108, 123, 139)));
    private static readonly Brush AxisBrush = Freeze(new SolidColorBrush(Color.FromArgb(132, 170, 185, 201)));
    private static readonly Brush GridBrush = Freeze(new SolidColorBrush(Color.FromArgb(62, 190, 207, 224)));
    private static readonly Brush PanelBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(138, 255, 255, 255),
        Color.FromArgb(58, 229, 237, 247),
        new Point(0, 0),
        new Point(1, 1)));
    private static readonly Brush CenterLightBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(76, 255, 255, 255),
        Color.FromArgb(0, 255, 255, 255))
    {
        Center = new Point(0.5, 0.4),
        GradientOrigin = new Point(0.5, 0.32),
        RadiusX = 0.58,
        RadiusY = 0.62
    });
    private static readonly Brush BackDepthBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 210, 224, 238), 0),
            new GradientStop(Color.FromArgb(24, 154, 184, 214), 0.58),
            new GradientStop(Color.FromArgb(46, 120, 154, 188), 1)
        }
    });
    private static readonly Brush EdgeLightBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 1),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(142, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(28, 195, 215, 235), 0.5),
            new GradientStop(Color.FromArgb(72, 170, 195, 222), 1)
        }
    });
    private static readonly Brush PlatformBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(238, 255, 255, 255),
        Color.FromArgb(232, 228, 237, 246),
        new Point(0, 0),
        new Point(0, 1)));
    private static readonly Brush FloorWashBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(42, 214, 226, 238), 0.58),
            new GradientStop(Color.FromArgb(70, 190, 207, 224), 1)
        }
    });
    private static readonly Brush FrontBrush = Freeze(CreateGradient("#B9EFF2", "#69BCD1", "#4F8FC0", vertical: true));
    private static readonly Brush FrontActiveBrush = Freeze(CreateGradient("#CDF6F7", "#77C7D8", "#5B9AC9", vertical: true));
    private static readonly Brush SideBrush = Freeze(CreateGradient("#8CDDE3", "#64AFC2", "#527F97", vertical: false));
    private static readonly Brush TopBrush = Freeze(CreateGradient("#F0FCFD", "#BAEEF2", "#8DD8DF", vertical: false));
    private static readonly Brush BaseShadowBrush = Freeze(new SolidColorBrush(Color.FromArgb(14, 70, 98, 126)));
    private static readonly Brush ValueBrush = Freeze(new SolidColorBrush(Color.FromArgb(126, 66, 139, 207)));
    private static readonly Brush SoftBorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(70, 198, 213, 226)));
    private static readonly Brush BarBorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(102, 111, 209, 231)));
    private static readonly Brush BarSideBorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(72, 86, 162, 190)));
    private static readonly Brush BarTopBorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(96, 172, 235, 242)));
    private static readonly Brush WhiteSheenBrush = Freeze(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)));
    private static readonly Brush HighlightLineBrush = Freeze(new SolidColorBrush(Color.FromArgb(58, 235, 252, 255)));
    private static readonly Brush PlatformBorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(132, 207, 219, 231)));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(DepartmentDocuments3DChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    private int _hoveredBarIndex = -1;
    private Rect[] _barHitRects = [];
    private IReadOnlyList<DepartmentItem> _items = [];
    private INotifyCollectionChanged? _observableItems;

    public DepartmentDocuments3DChart()
    {
        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Cursor = Cursors.Arrow;
        Unloaded += (_, _) => DetachCollectionChanged();
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
        for (var i = 0; i < _barHitRects.Length; i++)
        {
            if (_barHitRects[i].Contains(point))
            {
                hit = i;
                break;
            }
        }

        if (hit == _hoveredBarIndex)
        {
            return;
        }

        _hoveredBarIndex = hit;
        Cursor = hit >= 0 ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredBarIndex < 0)
        {
            return;
        }

        _hoveredBarIndex = -1;
        Cursor = Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 80 || height < 80)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var scale = Math.Min(width / 430.0, height / 270.0);
        var left = 35 * scale;
        var right = 18 * scale;
        var top = 20 * scale;
        var bottom = 38 * scale;
        var plotWidth = Math.Max(120 * scale, width - left - right);
        var plotHeight = Math.Max(92 * scale, (height - top - bottom) * 0.88);
        var baselineY = top + plotHeight;

        DrawGlassPanel(dc, left, top, plotWidth, plotHeight, scale);

        if (_items.Count == 0)
        {
            _barHitRects = [];
            DrawEmptyState(dc, left, top, plotWidth, plotHeight, scale, dpi);
            return;
        }

        var maxValue = GetNiceMax(_items.Max(item => item.Value));
        _barHitRects = new Rect[_items.Count];

        DrawGrid(dc, left, top, plotWidth, plotHeight, maxValue, scale, dpi);
        DrawPlatform(dc, left, baselineY, plotWidth, scale);
        DrawBars(dc, left, plotWidth, plotHeight, baselineY, maxValue, scale, dpi);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DepartmentDocuments3DChart chart)
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
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Name)
            .Take(6)
            .Select(item => new DepartmentItem(
                string.IsNullOrWhiteSpace(item.Name) ? "Khong xac dinh" : item.Name.Trim(),
                item.Value))
            .ToList()
            ?? [];

        _hoveredBarIndex = -1;
        _barHitRects = [];
        InvalidateVisual();
    }

    private void DrawGlassPanel(DrawingContext dc, double left, double top, double plotWidth, double plotHeight, double scale)
    {
        var rect = new Rect(left, top, plotWidth, plotHeight);
        dc.DrawRoundedRectangle(PanelBrush, new Pen(SoftBorderBrush, 0.55 * scale), rect, 7 * scale, 7 * scale);
        dc.DrawRoundedRectangle(CenterLightBrush, null, rect, 7 * scale, 7 * scale);
        dc.DrawRoundedRectangle(BackDepthBrush, null, rect, 7 * scale, 7 * scale);

        var innerRect = new Rect(left + 2 * scale, top + 2 * scale, Math.Max(1, plotWidth - 4 * scale), Math.Max(1, plotHeight - 4 * scale));
        dc.PushOpacity(0.62);
        dc.DrawRoundedRectangle(null, new Pen(EdgeLightBrush, 0.85 * scale), innerRect, 6 * scale, 6 * scale);
        dc.Pop();
    }

    private void DrawEmptyState(DrawingContext dc, double left, double top, double plotWidth, double plotHeight, double scale, double pixelsPerDip)
    {
        var message = Text("Chua co du lieu phong ban", 13 * scale, SemiBoldTypeface, MutedTextBrush, pixelsPerDip);
        dc.DrawText(message, new Point(left + (plotWidth - message.Width) / 2, top + (plotHeight - message.Height) / 2));
    }

    private void DrawGrid(DrawingContext dc, double left, double top, double plotWidth, double plotHeight, int maxValue, double scale, double pixelsPerDip)
    {
        var steps = maxValue <= 8
            ? maxValue
            : Math.Min(5, Math.Max(2, maxValue));
        for (var i = 0; i <= steps; i++)
        {
            var value = (int)Math.Round(maxValue * i / (double)steps);
            var y = top + plotHeight - plotHeight * i / steps;
            var label = Text(value.ToString(CultureInfo.CurrentCulture), 9.5 * scale, RegularTypeface, MutedTextBrush, pixelsPerDip);
            dc.DrawText(label, new Point(left - label.Width - 10 * scale, y - label.Height / 2));

            var pen = new Pen(GridBrush, i == 0 ? 0.75 * scale : 0.55 * scale);
            if (i != 0)
            {
                pen.DashStyle = new DashStyle([2.5, 4.0], 0);
            }

            dc.PushOpacity(i == 0 ? 0.52 : 0.28);
            dc.DrawLine(pen, new Point(left, y), new Point(left + plotWidth, y));
            dc.Pop();
        }

        dc.DrawLine(new Pen(AxisBrush, 0.65 * scale), new Point(left, top), new Point(left, top + plotHeight));
    }

    private void DrawBars(DrawingContext dc, double left, double plotWidth, double plotHeight, double baselineY, int maxValue, double scale, double pixelsPerDip)
    {
        var section = plotWidth / _items.Count;
        var barWidth = Math.Min(25 * scale, section * 0.2);
        var depthX = 5.5 * scale;
        var depthY = -3.5 * scale;

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var centerX = left + section * i + section / 2;
            var barHeight = Math.Max(4 * scale, plotHeight * item.Value / maxValue * 0.84);
            var x = centerX - barWidth / 2;
            var y = baselineY - barHeight;
            var active = i == _hoveredBarIndex;

            _barHitRects[i] = new Rect(x - 7 * scale, y + depthY - 7 * scale, barWidth + depthX + 15 * scale, barHeight - depthY + 18 * scale);
            DrawBarBase(dc, x, baselineY, barWidth, depthX, scale, active);
            Draw3DBar(dc, x, y, barWidth, barHeight, depthX, depthY, scale, active);
            DrawValueLabel(dc, item.Value, centerX + depthX * 0.18, y - 12 * scale, scale, pixelsPerDip, active);
            DrawCategoryLabel(dc, item.Label, centerX + depthX * 0.18, baselineY + 10 * scale, section, scale, pixelsPerDip);
        }
    }

    private void Draw3DBar(DrawingContext dc, double x, double y, double width, double height, double depthX, double depthY, double scale, bool active)
    {
        dc.PushOpacity(active ? 0.94 : 0.82);

        var frontRect = new Rect(x, y, width, height);
        dc.DrawRoundedRectangle(active ? FrontActiveBrush : FrontBrush, new Pen(BarBorderBrush, 0.55 * scale), frontRect, 2.4 * scale, 2.4 * scale);

        var side = new StreamGeometry();
        using (var ctx = side.Open())
        {
            ctx.BeginFigure(new Point(x + width, y), true, true);
            ctx.LineTo(new Point(x + width + depthX, y + depthY), true, false);
            ctx.LineTo(new Point(x + width + depthX, y + height + depthY), true, false);
            ctx.LineTo(new Point(x + width, y + height), true, false);
        }
        side.Freeze();
        dc.DrawGeometry(SideBrush, new Pen(BarSideBorderBrush, 0.45 * scale), side);

        var topFace = new StreamGeometry();
        using (var ctx = topFace.Open())
        {
            ctx.BeginFigure(new Point(x, y), true, true);
            ctx.LineTo(new Point(x + depthX, y + depthY), true, false);
            ctx.LineTo(new Point(x + width + depthX, y + depthY), true, false);
            ctx.LineTo(new Point(x + width, y), true, false);
        }
        topFace.Freeze();
        dc.DrawGeometry(TopBrush, new Pen(BarTopBorderBrush, 0.5 * scale), topFace);

        var diagonal = new StreamGeometry();
        using (var ctx = diagonal.Open())
        {
            ctx.BeginFigure(new Point(x + width * 0.13, y + height), true, true);
            ctx.LineTo(new Point(x + width * 0.79, y + 7 * scale), true, false);
            ctx.LineTo(new Point(x + width, y + 7 * scale), true, false);
            ctx.LineTo(new Point(x + width * 0.36, y + height), true, false);
        }
        diagonal.Freeze();
        dc.PushOpacity(active ? 0.42 : 0.26);
        dc.DrawGeometry(WhiteSheenBrush, null, diagonal);
        dc.Pop();

        dc.DrawLine(new Pen(HighlightLineBrush, 0.75 * scale), new Point(x + width - 1.4 * scale, y + 6 * scale), new Point(x + width - 1.4 * scale, y + height - 6 * scale));
        dc.Pop();
    }

    private void DrawBarBase(DrawingContext dc, double x, double baselineY, double width, double depthX, double scale, bool active)
    {
        dc.PushOpacity(active ? 0.24 : 0.14);
        dc.DrawEllipse(BaseShadowBrush, null, new Point(x + width / 2 + depthX / 2, baselineY + 4 * scale), (width + depthX + 10 * scale) / 2, 2.4 * scale);
        dc.Pop();

        var rect = new Rect(x - 3 * scale, baselineY - 1 * scale, width + depthX + 6 * scale, 5.5 * scale);
        dc.DrawRoundedRectangle(PlatformBrush, new Pen(PlatformBorderBrush, 0.4 * scale), rect, 2.2 * scale, 2.2 * scale);
    }

    private void DrawPlatform(DrawingContext dc, double left, double baselineY, double plotWidth, double scale)
    {
        var floorRect = new Rect(left - 6 * scale, baselineY - 18 * scale, plotWidth + 12 * scale, 24 * scale);
        dc.PushOpacity(0.48);
        dc.DrawRoundedRectangle(FloorWashBrush, null, floorRect, 6 * scale, 6 * scale);
        dc.Pop();

        var rect = new Rect(left - 6 * scale, baselineY - 2.5 * scale, plotWidth + 12 * scale, 7 * scale);
        dc.DrawRoundedRectangle(PlatformBrush, new Pen(PlatformBorderBrush, 0.4 * scale), rect, 3 * scale, 3 * scale);
    }

    private void DrawValueLabel(DrawingContext dc, int value, double centerX, double y, double scale, double pixelsPerDip, bool active)
    {
        var text = Text(value.ToString(CultureInfo.CurrentCulture), active ? 12 * scale : 11 * scale, MediumTypeface, ValueBrush, pixelsPerDip);
        dc.DrawText(text, new Point(centerX - text.Width / 2, y));
    }

    private void DrawCategoryLabel(DrawingContext dc, string label, double centerX, double y, double section, double scale, double pixelsPerDip)
    {
        var text = Text(label, 8.1 * scale, RegularTypeface, TextBrush, pixelsPerDip);
        text.MaxTextWidth = Math.Max(24 * scale, section - 10 * scale);
        text.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(text, new Point(centerX - text.Width / 2, y));
    }

    private static int GetNiceMax(int value)
    {
        if (value <= 4)
        {
            return 4;
        }

        var step = value <= 10 ? 2 : value <= 25 ? 5 : 10;
        return (int)Math.Ceiling(value / (double)step) * step;
    }

    private static FormattedText Text(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip)
    {
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, Math.Max(1, size), brush, pixelsPerDip);
    }

    private static Brush CreateGradient(string c1, string c2, string c3, bool vertical)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = vertical ? new Point(0, 1) : new Point(1, 1),
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString(c2), 0.46),
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

    private sealed record DepartmentItem(string Label, int Value);
}
