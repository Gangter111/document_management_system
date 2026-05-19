using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;

namespace DocumentManagement.Intelligence.Results;

public sealed record DocumentExtractionResult(
    ExtractedField<string> Issuer,
    ExtractedField<string> DocumentNumber,
    ExtractedField<DateOnly> IssueDate,
    ExtractedField<string> Title,
    ExtractedField<string> Signer,
    ExtractedField<string> Recipients,
    IReadOnlyList<BodySection> BodySections)
{
    public ExtractionDebugReport? DebugReport { get; init; }

    public IReadOnlyList<DocumentPageMetrics> PageMetrics { get; init; } = Array.Empty<DocumentPageMetrics>();
}
