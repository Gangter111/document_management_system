using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewOverlayRegion(
    string Id,
    string FieldName,
    int PageIndex,
    BoundingBox BoundingBox,
    double Confidence,
    string RejectReason,
    string SourceText,
    string BlockType,
    OverlayHighlightType HighlightType,
    string SemanticGroupId = "",
    bool IsPrimary = true,
    EvidenceRole Role = EvidenceRole.Primary,
    EvidenceProvenance? Provenance = null)
{
    public string Label =>
        HighlightType == OverlayHighlightType.Rejected && !string.IsNullOrWhiteSpace(RejectReason)
            ? $"{FieldName} - {Confidence:P0} - {RejectReason}"
            : $"{FieldName} - {Confidence:P0}";
}
