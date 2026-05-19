using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewEvidenceRegion(
    int PageIndex,
    BoundingBox? BoundingBox,
    string SourceText,
    string BlockType,
    int ReadingOrder,
    bool IsPrimary,
    EvidenceRole Role = EvidenceRole.Primary,
    EvidenceProvenance? Provenance = null,
    string SemanticGroupId = "");
