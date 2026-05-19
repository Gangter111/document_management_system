using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Review;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Wpf.Controls;

public sealed class DocumentReviewOverlayCanvas : FrameworkElement
{
    public static readonly DependencyProperty PageOverlayProperty =
        DependencyProperty.Register(
            nameof(PageOverlay),
            typeof(ReviewPageOverlay),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ActiveRegionIdProperty =
        DependencyProperty.Register(
            nameof(ActiveRegionId),
            typeof(string),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ActiveSemanticGroupIdProperty =
        DependencyProperty.Register(
            nameof(ActiveSemanticGroupId),
            typeof(string),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowAcceptedProperty =
        DependencyProperty.Register(
            nameof(ShowAccepted),
            typeof(bool),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowRejectedProperty =
        DependencyProperty.Register(
            nameof(ShowRejected),
            typeof(bool),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLowConfidenceOnlyProperty =
        DependencyProperty.Register(
            nameof(ShowLowConfidenceOnly),
            typeof(bool),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDebugOverlayEnabledProperty =
        DependencyProperty.Register(
            nameof(IsDebugOverlayEnabled),
            typeof(bool),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(
            nameof(ZoomFactor),
            typeof(double),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDiagnosticsEnabledProperty =
        DependencyProperty.Register(
            nameof(IsDiagnosticsEnabled),
            typeof(bool),
            typeof(DocumentReviewOverlayCanvas),
            new FrameworkPropertyMetadata(false));

    private static readonly Typeface LabelTypeface = new("Segoe UI");
    private static readonly Brush PageBrush = CreateBrush("#FFFFFFFF");
    private static readonly Brush PageBorderBrush = CreateBrush("#CBD5E1");
    private static readonly Brush TextBrush = CreateBrush("#0F172A");
    private static readonly Brush MutedTextBrush = CreateBrush("#475569");
    private static readonly Brush AcceptedFill = CreateBrush("#2E22C55E");
    private static readonly Brush AcceptedSupportingFill = CreateBrush("#1D22C55E");
    private static readonly Brush AcceptedContextualFill = CreateBrush("#1022C55E");
    private static readonly Brush AcceptedStroke = CreateBrush("#15803D");
    private static readonly Brush LowConfidenceFill = CreateBrush("#35F59E0B");
    private static readonly Brush LowConfidenceSupportingFill = CreateBrush("#22F59E0B");
    private static readonly Brush LowConfidenceContextualFill = CreateBrush("#12F59E0B");
    private static readonly Brush LowConfidenceStroke = CreateBrush("#B45309");
    private static readonly Brush RejectedFill = CreateBrush("#35EF4444");
    private static readonly Brush RejectedSupportingFill = CreateBrush("#22EF4444");
    private static readonly Brush RejectedContextualFill = CreateBrush("#12EF4444");
    private static readonly Brush RejectedStroke = CreateBrush("#B91C1C");
    private static readonly Brush ActiveStroke = CreateBrush("#0F172A");
    private static readonly Brush ActiveLabelBrush = CreateBrush("#EFFFFFFF");
    private static readonly Brush LabelBackgroundBrush = CreateBrush("#FFFFFFFF");
    private static readonly Pen PageBorderPen = CreatePen(PageBorderBrush, 1);
    private static readonly Pen AcceptedPen = CreatePen(AcceptedStroke, 1.5);
    private static readonly Pen LowConfidencePen = CreatePen(LowConfidenceStroke, 1.5);
    private static readonly Pen RejectedPen = CreatePen(RejectedStroke, 1.5);
    private static readonly Pen AcceptedSupportingPen = CreatePen(AcceptedStroke, 1.1);
    private static readonly Pen LowConfidenceSupportingPen = CreatePen(LowConfidenceStroke, 1.1);
    private static readonly Pen RejectedSupportingPen = CreatePen(RejectedStroke, 1.1);
    private static readonly Pen AcceptedContextualPen = CreatePen(AcceptedStroke, 1, new DoubleCollection { 4, 3 });
    private static readonly Pen LowConfidenceContextualPen = CreatePen(LowConfidenceStroke, 1, new DoubleCollection { 4, 3 });
    private static readonly Pen RejectedContextualPen = CreatePen(RejectedStroke, 1, new DoubleCollection { 4, 3 });
    private static readonly Pen AcceptedGroupPen = CreatePen(AcceptedStroke, 2.2);
    private static readonly Pen LowConfidenceGroupPen = CreatePen(LowConfidenceStroke, 2.2);
    private static readonly Pen RejectedGroupPen = CreatePen(RejectedStroke, 2.2);
    private static readonly Pen ActivePen = CreatePen(ActiveStroke, 3);
    private static readonly Pen AcceptedLabelPen = CreatePen(AcceptedStroke, 1);
    private static readonly Pen LowConfidenceLabelPen = CreatePen(LowConfidenceStroke, 1);
    private static readonly Pen RejectedLabelPen = CreatePen(RejectedStroke, 1);

    private ReviewPageOverlay? _cachedPage;
    private string _cachedPageSignature = string.Empty;
    private RenderRegion[] _renderRegions = Array.Empty<RenderRegion>();
    private Rect _pageRect;
    private double _cachedWidth = -1;
    private double _cachedHeight = -1;
    private double _cachedPixelsPerDip = -1;
    private bool _cachedShowAccepted;
    private bool _cachedShowRejected;
    private bool _cachedShowLowConfidenceOnly;
    private bool _cachedDebugOverlay;
    private double _cachedZoomFactor = -1;
    private bool _hasMappedBounds;

    public long CacheRebuildCount { get; private set; }

    public int LastVisibleOverlayCount { get; private set; }

    public TimeSpan LastRenderDuration { get; private set; }

    public string LastInvalidationReason { get; private set; } = string.Empty;

    public event EventHandler<ReviewOverlayRegion>? RegionClicked;

    public ReviewPageOverlay? PageOverlay
    {
        get => (ReviewPageOverlay?)GetValue(PageOverlayProperty);
        set => SetValue(PageOverlayProperty, value);
    }

    public string? ActiveRegionId
    {
        get => (string?)GetValue(ActiveRegionIdProperty);
        set => SetValue(ActiveRegionIdProperty, value);
    }

    public string? ActiveSemanticGroupId
    {
        get => (string?)GetValue(ActiveSemanticGroupIdProperty);
        set => SetValue(ActiveSemanticGroupIdProperty, value);
    }

    public bool ShowAccepted
    {
        get => (bool)GetValue(ShowAcceptedProperty);
        set => SetValue(ShowAcceptedProperty, value);
    }

    public bool ShowRejected
    {
        get => (bool)GetValue(ShowRejectedProperty);
        set => SetValue(ShowRejectedProperty, value);
    }

    public bool ShowLowConfidenceOnly
    {
        get => (bool)GetValue(ShowLowConfidenceOnlyProperty);
        set => SetValue(ShowLowConfidenceOnlyProperty, value);
    }

    public bool IsDebugOverlayEnabled
    {
        get => (bool)GetValue(IsDebugOverlayEnabledProperty);
        set => SetValue(IsDebugOverlayEnabledProperty, value);
    }

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    public bool IsDiagnosticsEnabled
    {
        get => (bool)GetValue(IsDiagnosticsEnabledProperty);
        set => SetValue(IsDiagnosticsEnabledProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var stopwatch = IsDiagnosticsEnabled ? Stopwatch.StartNew() : null;
        base.OnRender(drawingContext);

        var page = PageOverlay;
        if (page is null || page.SourceWidth <= 0 || page.SourceHeight <= 0)
        {
            DrawEmptyState(drawingContext);
            FinishDiagnostics(stopwatch);
            return;
        }

        EnsureRenderCache(page);
        drawingContext.DrawRectangle(PageBrush, PageBorderPen, _pageRect);

        for (var i = 0; i < _renderRegions.Length; i++)
        {
            DrawRegion(drawingContext, _renderRegions[i]);
        }

        FinishDiagnostics(stopwatch);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        var page = PageOverlay;
        if (page is null)
        {
            return;
        }

        EnsureRenderCache(page);

        var point = e.GetPosition(this);
        for (var i = _renderRegions.Length - 1; i >= 0; i--)
        {
            var candidate = _renderRegions[i];
            if (!candidate.Bounds.Contains(point))
            {
                continue;
            }

            RegionClicked?.Invoke(this, candidate.Region);
            e.Handled = true;
            return;
        }
    }

    private void DrawRegion(DrawingContext drawingContext, RenderRegion cached)
    {
        var isPrimaryActive = string.Equals(cached.Region.Id, ActiveRegionId, StringComparison.Ordinal);
        var isGroupActive = !string.IsNullOrWhiteSpace(ActiveSemanticGroupId) &&
            string.Equals(cached.Region.SemanticGroupId, ActiveSemanticGroupId, StringComparison.Ordinal);
        drawingContext.DrawRoundedRectangle(cached.Fill, isPrimaryActive ? ActivePen : isGroupActive ? cached.GroupPen : cached.RegionPen, cached.Bounds, 2, 2);

        var formatted = isPrimaryActive ? cached.ActiveText : cached.NormalText;
        var labelX = Math.Max(cached.Bounds.Left, 6);
        var labelY = Math.Max(6, cached.Bounds.Top - formatted.Height - 4);
        var labelRect = new Rect(
            labelX - 4,
            labelY - 2,
            Math.Min(formatted.Width + 8, Math.Max(60, ActualWidth - labelX - 8)),
            formatted.Height + 4);

        drawingContext.DrawRoundedRectangle(isPrimaryActive ? ActiveLabelBrush : LabelBackgroundBrush, cached.LabelPen, labelRect, 2, 2);
        formatted.MaxTextWidth = Math.Max(40, labelRect.Width - 8);
        drawingContext.DrawText(formatted, new Point(labelRect.Left + 4, labelRect.Top + 2));
    }

    private void DrawEmptyState(DrawingContext drawingContext)
    {
        var text = CreateText("No source regions available", 14, MutedTextBrush, FontWeights.SemiBold, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(text, new Point(Math.Max(0, (ActualWidth - text.Width) / 2), Math.Max(0, (ActualHeight - text.Height) / 2)));
    }

    private void EnsureRenderCache(ReviewPageOverlay page)
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var pageSignature = CreatePageSignature(page);
        var staticCacheValid =
            ReferenceEquals(_cachedPage, page) &&
            string.Equals(_cachedPageSignature, pageSignature, StringComparison.Ordinal) &&
            NearlyEqual(_cachedPixelsPerDip, pixelsPerDip) &&
            _cachedShowAccepted == ShowAccepted &&
            _cachedShowRejected == ShowRejected &&
            _cachedShowLowConfidenceOnly == ShowLowConfidenceOnly &&
            _cachedDebugOverlay == IsDebugOverlayEnabled;

        if (!staticCacheValid)
        {
            RebuildRegionCache(page, pixelsPerDip, pageSignature);
        }

        if (_hasMappedBounds &&
            NearlyEqual(_cachedWidth, ActualWidth) &&
            NearlyEqual(_cachedHeight, ActualHeight) &&
            NearlyEqual(_cachedZoomFactor, SanitizedZoomFactor))
        {
            return;
        }

        UpdateMappedBounds(page);
    }

    private void RebuildRegionCache(ReviewPageOverlay page, double pixelsPerDip, string pageSignature)
    {
        LastInvalidationReason = ResolveInvalidationReason(page, pixelsPerDip, pageSignature);
        CacheRebuildCount++;
        _cachedPage = page;
        _cachedPageSignature = pageSignature;
        _cachedPixelsPerDip = pixelsPerDip;
        _cachedShowAccepted = ShowAccepted;
        _cachedShowRejected = ShowRejected;
        _cachedShowLowConfidenceOnly = ShowLowConfidenceOnly;
        _cachedDebugOverlay = IsDebugOverlayEnabled;
        _hasMappedBounds = false;

        var regions = page.Regions;
        var visibleCount = 0;
        for (var i = 0; i < regions.Count; i++)
        {
            if (IsRegionVisible(regions[i]))
            {
                visibleCount++;
            }
        }

        if (visibleCount == 0)
        {
            _renderRegions = Array.Empty<RenderRegion>();
            LastVisibleOverlayCount = 0;
            LogDiagnostics();
            return;
        }

        var renderRegions = new RenderRegion[visibleCount];
        var renderIndex = 0;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (!IsRegionVisible(region))
            {
                continue;
            }

            var label = IsDebugOverlayEnabled
                ? $"{region.Label} | {region.Role} | {region.Provenance?.SemanticRole} | {region.BlockType} | {Format(region.BoundingBox)} | {Trim(region.SourceText, 90)}"
                : region.Label;

            renderRegions[renderIndex++] = new RenderRegion(
                region,
                GetFill(region.HighlightType, region.Role),
                GetRegionPen(region.HighlightType, region.Role),
                GetGroupPen(region.HighlightType),
                GetLabelPen(region.HighlightType),
                CreateText(label, 11, TextBrush, FontWeights.Normal, pixelsPerDip),
                CreateText(label, 12, TextBrush, FontWeights.SemiBold, pixelsPerDip));
        }

        _renderRegions = renderRegions;
        LastVisibleOverlayCount = _renderRegions.Length;
        LogDiagnostics();
    }

    private void UpdateMappedBounds(ReviewPageOverlay page)
    {
        _cachedWidth = ActualWidth;
        _cachedHeight = ActualHeight;
        _cachedZoomFactor = SanitizedZoomFactor;
        _pageRect = MapToViewportRect(0, 0, page.SourceWidth, page.SourceHeight, page.SourceWidth, page.SourceHeight);

        for (var i = 0; i < _renderRegions.Length; i++)
        {
            var box = _renderRegions[i].Region.BoundingBox;
            _renderRegions[i].Bounds = MapToViewportRect(box.X1, box.Y1, box.X2, box.Y2, page.SourceWidth, page.SourceHeight);
        }

        _hasMappedBounds = true;
    }

    private bool IsRegionVisible(ReviewOverlayRegion region)
    {
        if (ShowLowConfidenceOnly)
        {
            return region.HighlightType == OverlayHighlightType.LowConfidence;
        }

        return region.HighlightType switch
        {
            OverlayHighlightType.Accepted => ShowAccepted,
            OverlayHighlightType.LowConfidence => ShowAccepted,
            OverlayHighlightType.Rejected => ShowRejected,
            _ => false
        };
    }

    private Rect MapToViewportRect(double x1, double y1, double x2, double y2, double sourceWidth, double sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        var scale = Math.Min(ActualWidth / sourceWidth, ActualHeight / sourceHeight) * SanitizedZoomFactor;
        var offsetX = (ActualWidth - (sourceWidth * scale)) / 2;
        var offsetY = (ActualHeight - (sourceHeight * scale)) / 2;
        var left = offsetX + (x1 * scale);
        var top = offsetY + (y1 * scale);
        var right = offsetX + (x2 * scale);
        var bottom = offsetY + (y2 * scale);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static Brush GetFill(OverlayHighlightType type, EvidenceRole role) =>
        (type, role) switch
        {
            (OverlayHighlightType.Accepted, EvidenceRole.Supporting) => AcceptedSupportingFill,
            (OverlayHighlightType.Accepted, EvidenceRole.Contextual) => AcceptedContextualFill,
            (OverlayHighlightType.Accepted, _) => AcceptedFill,
            (OverlayHighlightType.LowConfidence, EvidenceRole.Supporting) => LowConfidenceSupportingFill,
            (OverlayHighlightType.LowConfidence, EvidenceRole.Contextual) => LowConfidenceContextualFill,
            (OverlayHighlightType.LowConfidence, _) => LowConfidenceFill,
            (OverlayHighlightType.Rejected, EvidenceRole.Supporting) => RejectedSupportingFill,
            (OverlayHighlightType.Rejected, EvidenceRole.Contextual) => RejectedContextualFill,
            (OverlayHighlightType.Rejected, _) => RejectedFill,
            _ => LowConfidenceFill
        };

    private static Pen GetRegionPen(OverlayHighlightType type, EvidenceRole role) =>
        (type, role) switch
        {
            (OverlayHighlightType.Accepted, EvidenceRole.Supporting) => AcceptedSupportingPen,
            (OverlayHighlightType.Accepted, EvidenceRole.Contextual) => AcceptedContextualPen,
            (OverlayHighlightType.Accepted, _) => AcceptedPen,
            (OverlayHighlightType.LowConfidence, EvidenceRole.Supporting) => LowConfidenceSupportingPen,
            (OverlayHighlightType.LowConfidence, EvidenceRole.Contextual) => LowConfidenceContextualPen,
            (OverlayHighlightType.LowConfidence, _) => LowConfidencePen,
            (OverlayHighlightType.Rejected, EvidenceRole.Supporting) => RejectedSupportingPen,
            (OverlayHighlightType.Rejected, EvidenceRole.Contextual) => RejectedContextualPen,
            (OverlayHighlightType.Rejected, _) => RejectedPen,
            _ => LowConfidencePen
        };

    private static Pen GetGroupPen(OverlayHighlightType type) =>
        type switch
        {
            OverlayHighlightType.Accepted => AcceptedGroupPen,
            OverlayHighlightType.LowConfidence => LowConfidenceGroupPen,
            OverlayHighlightType.Rejected => RejectedGroupPen,
            _ => LowConfidenceGroupPen
        };

    private static Pen GetLabelPen(OverlayHighlightType type) =>
        type switch
        {
            OverlayHighlightType.Accepted => AcceptedLabelPen,
            OverlayHighlightType.LowConfidence => LowConfidenceLabelPen,
            OverlayHighlightType.Rejected => RejectedLabelPen,
            _ => LowConfidenceLabelPen
        };

    private static FormattedText CreateText(string text, double size, Brush brush, FontWeight weight, double pixelsPerDip) =>
        new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(LabelTypeface.FontFamily, FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            pixelsPerDip);

    private static string Format(BoundingBox box) =>
        $"[{box.X1:0},{box.Y1:0},{box.X2:0},{box.Y2:0}]";

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static Brush CreateBrush(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness, DoubleCollection? dashArray = null)
    {
        var pen = new Pen(brush, thickness);
        if (dashArray is not null)
        {
            pen.DashStyle = new DashStyle(dashArray, 0);
        }

        pen.Freeze();
        return pen;
    }

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) < 0.01;

    private double SanitizedZoomFactor => double.IsFinite(ZoomFactor) && ZoomFactor > 0
        ? Math.Min(4, Math.Max(0.25, ZoomFactor))
        : 1;

    private void FinishDiagnostics(Stopwatch? stopwatch)
    {
        if (stopwatch is null)
        {
            return;
        }

        stopwatch.Stop();
        LastRenderDuration = stopwatch.Elapsed;
    }

    private string ResolveInvalidationReason(ReviewPageOverlay page, double pixelsPerDip, string pageSignature)
    {
        if (!ReferenceEquals(_cachedPage, page))
        {
            return "page";
        }

        if (!string.Equals(_cachedPageSignature, pageSignature, StringComparison.Ordinal))
        {
            return "overlay-remap";
        }

        if (!NearlyEqual(_cachedPixelsPerDip, pixelsPerDip))
        {
            return "dpi";
        }

        if (_cachedShowAccepted != ShowAccepted ||
            _cachedShowRejected != ShowRejected ||
            _cachedShowLowConfidenceOnly != ShowLowConfidenceOnly)
        {
            return "filter";
        }

        if (_cachedDebugOverlay != IsDebugOverlayEnabled)
        {
            return "debug";
        }

        return "unknown";
    }

    private static string CreatePageSignature(ReviewPageOverlay page)
    {
        unchecked
        {
            var hash = 17;
            Add(ref hash, page.PageIndex);
            Add(ref hash, page.SourceWidth);
            Add(ref hash, page.SourceHeight);
            Add(ref hash, page.Regions.Count);
            for (var i = 0; i < page.Regions.Count; i++)
            {
                var region = page.Regions[i];
                Add(ref hash, region.Id);
                Add(ref hash, region.SemanticGroupId);
                Add(ref hash, (int)region.Role);
                Add(ref hash, (int)region.HighlightType);
                Add(ref hash, region.IsPrimary ? 1 : 0);
            }

            return hash.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static void Add(ref int hash, int value)
    {
        unchecked
        {
            hash = (hash * 31) + value;
        }
    }

    private static void Add(ref int hash, double value) =>
        Add(ref hash, unchecked((int)(BitConverter.DoubleToInt64Bits(value) ^ (BitConverter.DoubleToInt64Bits(value) >> 32))));

    private static void Add(ref int hash, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            Add(ref hash, value[i]);
        }
    }

    private void LogDiagnostics()
    {
        if (!IsDiagnosticsEnabled)
        {
            return;
        }

        Debug.WriteLine(
            $"DocumentReviewOverlayCanvas cache rebuild #{CacheRebuildCount}: reason={LastInvalidationReason}, visible={LastVisibleOverlayCount}");
    }

    private sealed class RenderRegion
    {
        public RenderRegion(
            ReviewOverlayRegion region,
            Brush fill,
            Pen regionPen,
            Pen groupPen,
            Pen labelPen,
            FormattedText normalText,
            FormattedText activeText)
        {
            Region = region;
            Fill = fill;
            RegionPen = regionPen;
            GroupPen = groupPen;
            LabelPen = labelPen;
            NormalText = normalText;
            ActiveText = activeText;
        }

        public ReviewOverlayRegion Region { get; }

        public Rect Bounds { get; set; }

        public Brush Fill { get; }

        public Pen RegionPen { get; }

        public Pen GroupPen { get; }

        public Pen LabelPen { get; }

        public FormattedText NormalText { get; }

        public FormattedText ActiveText { get; }
    }
}
