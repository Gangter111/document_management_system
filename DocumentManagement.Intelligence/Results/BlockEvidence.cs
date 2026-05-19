using DocumentManagement.Intelligence.Models;

namespace DocumentManagement.Intelligence.Results;

public sealed record BlockEvidence(
    int PageNumber,
    int ReadingOrder,
    string BlockType,
    string RawText,
    string NormalizedText,
    BoundingBox BoundingBox,
    EvidenceRole Role = EvidenceRole.Primary,
    string ExtractionSource = "",
    string RejectSource = "",
    string SemanticRole = "")
{
    public EvidenceProvenance Provenance =>
        new(ExtractionSource, RejectSource, RawText, NormalizedText, SemanticRole);

    public static BlockEvidence FromBlock(
        DocumentBlock block,
        EvidenceRole role = EvidenceRole.Primary,
        string extractionSource = "",
        string rejectSource = "",
        string semanticRole = "") =>
        new(
            block.PageNumber,
            block.ReadingOrder,
            block.BlockType,
            block.RawText,
            block.NormalizedText,
            block.BoundingBox,
            role,
            extractionSource,
            rejectSource,
            semanticRole);
}
