using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Review;

namespace DocumentManagement.Wpf.ViewModels;

public sealed class ReviewFieldItemViewModel
{
    public ReviewFieldItemViewModel(ReviewField field)
    {
        Name = field.Name;
        Value = string.IsNullOrWhiteSpace(field.Value) ? "(empty)" : field.Value;
        Confidence = field.Confidence;
        Status = field.Status.ToString();
        Reasoning = string.Join(Environment.NewLine, field.Reasoning);
        SourceText = field.SourceText;
        SourceBlockType = field.SourceBlockType;
        SourceBoundingBox = field.SourceBoundingBox;
        EvidenceRegions = field.EvidenceRegions;
        SourceRegion = FormatRegion(field.SourceBoundingBox);
        PageIndex = field.PageIndex;
    }

    public string Name { get; }

    public string Value { get; }

    public double Confidence { get; }

    public string ConfidencePercent => $"{Confidence:P0}";

    public string ConfidenceBrush => Confidence >= 0.85 ? "#15803D" : Confidence >= 0.60 ? "#B45309" : "#B91C1C";

    public string ConfidenceBackgroundBrush => Confidence >= 0.85 ? "#DCFCE7" : Confidence >= 0.60 ? "#FEF3C7" : "#FEE2E2";

    public string Status { get; }

    public string Reasoning { get; }

    public string SourceText { get; }

    public string SourceBlockType { get; }

    public BoundingBox? SourceBoundingBox { get; }

    public IReadOnlyList<ReviewEvidenceRegion> EvidenceRegions { get; }

    public string SourceRegion { get; }

    public int PageIndex { get; }

    private static string FormatRegion(BoundingBox? bbox) =>
        bbox is null
            ? "No bbox"
            : $"x1={bbox.X1:0}, y1={bbox.Y1:0}, x2={bbox.X2:0}, y2={bbox.Y2:0}";
}
