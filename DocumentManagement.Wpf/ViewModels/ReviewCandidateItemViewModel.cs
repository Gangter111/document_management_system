using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Review;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Wpf.ViewModels;

public sealed class ReviewCandidateItemViewModel
{
    public ReviewCandidateItemViewModel(ReviewCandidate candidate)
    {
        FieldName = candidate.FieldName;
        Value = candidate.Value;
        Confidence = candidate.Confidence;
        Status = candidate.Status.ToString();
        Reasoning = string.Join(Environment.NewLine, candidate.Reasoning);
        RejectReasons = string.Join(Environment.NewLine, candidate.RejectReasons);
        SourceText = candidate.SourceText;
        SourceBlockType = candidate.SourceBlockType;
        SourceBoundingBox = candidate.SourceBoundingBox;
        EvidenceRegions = candidate.EvidenceRegions;
        EvidenceItems = candidate.EvidenceRegions
            .Select((evidence, index) => new ReviewEvidenceDisplayItem(evidence, index + 1))
            .ToArray();
        ReasoningItems = (candidate.ReasoningChain ?? Array.Empty<ReviewReasoningStep>())
            .Select(step => new ReviewReasoningDisplayItem(step))
            .ToArray();
        ConfidenceItems = (candidate.ConfidenceBreakdown ?? Array.Empty<ReviewConfidenceContribution>())
            .Select(contribution => new ReviewConfidenceDisplayItem(contribution))
            .ToArray();
        ProvenanceText = EvidenceItems.Count == 0
            ? "No evidence provenance available."
            : string.Join(Environment.NewLine + Environment.NewLine, EvidenceItems.Select(item => item.ProvenanceText));
        var focusEvidence = ReviewEvidenceResolver.ResolvePrimary(candidate.EvidenceRegions, requireDrawable: true);
        FocusBoundingBox = focusEvidence?.BoundingBox ?? candidate.SourceBoundingBox;
        FocusPageIndex = focusEvidence?.PageIndex ?? candidate.PageIndex;
        SourceRegion = FormatRegion(candidate.SourceBoundingBox);
        PageIndex = candidate.PageIndex;
    }

    public string FieldName { get; }

    public string Value { get; }

    public double Confidence { get; }

    public string ConfidencePercent => $"{Confidence:P0}";

    public string ConfidenceBrush => Confidence >= 0.85 ? "#15803D" : Confidence >= 0.60 ? "#B45309" : "#B91C1C";

    public string ConfidenceBackgroundBrush => Confidence >= 0.85 ? "#DCFCE7" : Confidence >= 0.60 ? "#FEF3C7" : "#FEE2E2";

    public string Status { get; }

    public string Reasoning { get; }

    public string RejectReasons { get; }

    public string SourceText { get; }

    public string SourceBlockType { get; }

    public BoundingBox? SourceBoundingBox { get; }

    public IReadOnlyList<ReviewEvidenceRegion> EvidenceRegions { get; }

    public IReadOnlyList<ReviewEvidenceDisplayItem> EvidenceItems { get; }

    public IReadOnlyList<ReviewReasoningDisplayItem> ReasoningItems { get; }

    public IReadOnlyList<ReviewConfidenceDisplayItem> ConfidenceItems { get; }

    public string ProvenanceText { get; }

    public string SourceRegion { get; }

    public int PageIndex { get; }

    public BoundingBox? FocusBoundingBox { get; }

    public int FocusPageIndex { get; }

    private static string FormatRegion(BoundingBox? bbox) =>
        bbox is null
            ? "No bbox; retained for provenance only"
            : $"x1={bbox.X1:0}, y1={bbox.Y1:0}, x2={bbox.X2:0}, y2={bbox.Y2:0}";
}

public sealed class ReviewReasoningDisplayItem
{
    public ReviewReasoningDisplayItem(ReviewReasoningStep step)
    {
        Category = step.Category;
        Text = step.Text;
        CategoryBrush = step.Category switch
        {
            "Final decision" => "#0F172A",
            "Rejected evidence" => "#B91C1C",
            "Supporting evidence" => "#334155",
            _ => "#475569"
        };
        CategoryBackgroundBrush = step.Category switch
        {
            "Final decision" => "#DBEAFE",
            "Rejected evidence" => "#FEE2E2",
            "Supporting evidence" => "#E2E8F0",
            _ => "#F8FAFC"
        };
    }

    public string Category { get; }

    public string Text { get; }

    public string CategoryBrush { get; }

    public string CategoryBackgroundBrush { get; }
}

public sealed class ReviewConfidenceDisplayItem
{
    public ReviewConfidenceDisplayItem(ReviewConfidenceContribution contribution)
    {
        Name = contribution.Name;
        Value = contribution.Value;
        ValueText = contribution.Value >= 0
            ? $"+{contribution.Value:0.00}"
            : $"{contribution.Value:0.00}";
        Explanation = contribution.Explanation;
        ValueBrush = contribution.Value < 0 ? "#B91C1C" : "#15803D";
    }

    public string Name { get; }

    public double Value { get; }

    public string ValueText { get; }

    public string Explanation { get; }

    public string ValueBrush { get; }
}

public sealed class ReviewEvidenceDisplayItem
{
    public ReviewEvidenceDisplayItem(ReviewEvidenceRegion evidence, int index)
    {
        Index = index;
        Role = evidence.Role.ToString();
        RawText = evidence.Provenance?.RawText ?? string.Empty;
        NormalizedText = string.IsNullOrWhiteSpace(evidence.Provenance?.NormalizedText)
            ? evidence.SourceText
            : evidence.Provenance!.NormalizedText;
        ExtractionSource = string.IsNullOrWhiteSpace(evidence.Provenance?.ExtractionSource)
            ? "(not recorded)"
            : evidence.Provenance!.ExtractionSource;
        RejectSource = string.IsNullOrWhiteSpace(evidence.Provenance?.RejectSource)
            ? "(none)"
            : evidence.Provenance!.RejectSource;
        SemanticRole = string.IsNullOrWhiteSpace(evidence.Provenance?.SemanticRole)
            ? "(unspecified)"
            : evidence.Provenance!.SemanticRole;
        SourceRegion = FormatRegion(evidence.BoundingBox);
        ProvenanceText = ReviewEvidenceProvenanceFormatter.Format(evidence);
        RoleBrush = evidence.Role switch
        {
            EvidenceRole.Primary => "#0F172A",
            EvidenceRole.Supporting => "#334155",
            _ => "#64748B"
        };
        RoleBackgroundBrush = evidence.Role switch
        {
            EvidenceRole.Primary => "#DBEAFE",
            EvidenceRole.Supporting => "#E2E8F0",
            _ => "#F1F5F9"
        };
        FontWeight = evidence.Role == EvidenceRole.Primary ? "SemiBold" : "Normal";
    }

    public int Index { get; }

    public string Role { get; }

    public string RawText { get; }

    public string NormalizedText { get; }

    public string ExtractionSource { get; }

    public string RejectSource { get; }

    public string SemanticRole { get; }

    public string SourceRegion { get; }

    public string ProvenanceText { get; }

    public string RoleBrush { get; }

    public string RoleBackgroundBrush { get; }

    public string FontWeight { get; }

    private static string FormatRegion(BoundingBox? bbox) =>
        bbox is null
            ? "No bbox; retained for provenance only"
            : $"x1={bbox.X1:0}, y1={bbox.Y1:0}, x2={bbox.X2:0}, y2={bbox.Y2:0}";
}
