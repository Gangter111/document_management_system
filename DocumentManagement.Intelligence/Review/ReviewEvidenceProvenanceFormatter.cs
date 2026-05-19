using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public static class ReviewEvidenceProvenanceFormatter
{
    public static string Format(ReviewEvidenceRegion evidence)
    {
        var provenance = evidence.Provenance ?? new EvidenceProvenance(NormalizedText: evidence.SourceText);
        var role = evidence.Role.ToString();
        var rawText = string.IsNullOrWhiteSpace(provenance.RawText) ? "(empty)" : provenance.RawText;
        var normalizedText = string.IsNullOrWhiteSpace(provenance.NormalizedText)
            ? evidence.SourceText
            : provenance.NormalizedText;
        var extractionSource = string.IsNullOrWhiteSpace(provenance.ExtractionSource)
            ? "(not recorded)"
            : provenance.ExtractionSource;
        var rejectSource = string.IsNullOrWhiteSpace(provenance.RejectSource)
            ? "(none)"
            : provenance.RejectSource;
        var semanticRole = string.IsNullOrWhiteSpace(provenance.SemanticRole)
            ? "(unspecified)"
            : provenance.SemanticRole;
        var diagnostics = provenance.Diagnostics.Count == 0
            ? "(none)"
            : string.Join("; ", provenance.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
        var bbox = evidence.BoundingBox is null
            ? "No bbox; evidence is retained for provenance but has no drawable overlay region from MinerU."
            : $"x1={evidence.BoundingBox.X1:0}, y1={evidence.BoundingBox.Y1:0}, x2={evidence.BoundingBox.X2:0}, y2={evidence.BoundingBox.Y2:0}";

        return string.Join(
            Environment.NewLine,
            $"Role: {role}",
            $"Raw OCR: {rawText}",
            $"Normalized: {normalizedText}",
            $"Extraction: {extractionSource}",
            $"Reject: {rejectSource}",
            $"Semantic: {semanticRole}",
            $"Diagnostics: {diagnostics}",
            $"Region: {bbox}");
    }
}
