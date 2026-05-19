using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Confidence;

public sealed record ExtractionCandidate<T>(
    string FieldName,
    T? Value,
    string CandidateText,
    ConfidenceScore Confidence,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<RejectReason> RejectReasons,
    IReadOnlyList<BlockEvidence> Evidence) : IExtractionCandidate
{
    public bool IsRejected => RejectReasons.Count > 0 || Confidence.Value <= 0;

    public bool IsHardRejected => RejectReasons.Any(reason => reason.IsHardReject);

    public string DisplayValue => Value is DateOnly date ? date.ToString("yyyy-MM-dd") : Value?.ToString() ?? string.Empty;

    public bool IsAccepted(double minimumConfidence) =>
        !IsRejected && Confidence.Value >= minimumConfidence && Value is not null;

    public static ExtractionCandidate<T> Rejected(
        string fieldName,
        string candidateText,
        RejectReason reason,
        IReadOnlyList<BlockEvidence> evidence) =>
        new(
            fieldName,
            default,
            candidateText,
            ConfidenceScore.Zero(reason.Message),
            new[] { reason.Message },
            new[] { reason },
            evidence);
}
