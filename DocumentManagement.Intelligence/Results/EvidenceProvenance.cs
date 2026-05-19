namespace DocumentManagement.Intelligence.Results;

public sealed record EvidenceProvenance(
    string ExtractionSource = "",
    string RejectSource = "",
    string RawText = "",
    string NormalizedText = "",
    string SemanticRole = "",
    IReadOnlyList<EvidenceDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<EvidenceDiagnostic> Diagnostics { get; init; } = Diagnostics ?? Array.Empty<EvidenceDiagnostic>();
}
