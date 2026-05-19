using DocumentManagement.Intelligence.Models;

namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewCandidate(
    string FieldName,
    string Value,
    double Confidence,
    ReviewStatus Status,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> RejectReasons,
    string SourceText,
    string SourceBlockType,
    BoundingBox? SourceBoundingBox,
    int PageIndex,
    IReadOnlyList<ReviewEvidenceRegion> EvidenceRegions,
    IReadOnlyList<ReviewReasoningStep>? ReasoningChain = null,
    IReadOnlyList<ReviewConfidenceContribution>? ConfidenceBreakdown = null);
