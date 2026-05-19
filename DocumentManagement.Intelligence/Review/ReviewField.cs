using DocumentManagement.Intelligence.Models;

namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewField(
    string Name,
    string Value,
    double Confidence,
    ReviewStatus Status,
    IReadOnlyList<string> Reasoning,
    string SourceText,
    string SourceBlockType,
    BoundingBox? SourceBoundingBox,
    int PageIndex,
    IReadOnlyList<ReviewEvidenceRegion> EvidenceRegions,
    IReadOnlyList<ReviewCandidate> Candidates,
    IReadOnlyList<ReviewReasoningStep>? ReasoningChain = null,
    IReadOnlyList<ReviewConfidenceContribution>? ConfidenceBreakdown = null);
