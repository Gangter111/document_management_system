using DocumentManagement.Intelligence.Confidence;

namespace DocumentManagement.Intelligence.Results;

public sealed record ExtractedField<T>(
    T? Value,
    ConfidenceScore ConfidenceScore,
    string Reason,
    IReadOnlyList<BlockEvidence> Evidence,
    IReadOnlyList<ExtractionCandidate<T>> Candidates)
{
    public double Confidence => ConfidenceScore.Value;

    public IReadOnlyList<RejectReason> RejectReasons =>
        Candidates.SelectMany(candidate => candidate.RejectReasons).ToArray();

    public bool HasValue => Confidence > 0 && !EqualityComparer<T?>.Default.Equals(Value, default);

    public static ExtractedField<T> Empty(string reason, IReadOnlyList<BlockEvidence>? evidence = null) =>
        new(default, ConfidenceScore.Zero(reason), reason, evidence ?? Array.Empty<BlockEvidence>(), Array.Empty<ExtractionCandidate<T>>());

    public ExtractedField<T> RequireConfidence(double minimumConfidence, string lowConfidenceReason) =>
        Confidence >= minimumConfidence ? this : this with { Value = default, Reason = lowConfidenceReason };

    public static ExtractedField<T> FromCandidate(ExtractionCandidate<T> candidate, double minimumConfidence, string lowConfidenceReason)
    {
        if (!candidate.IsAccepted(minimumConfidence))
        {
            return new ExtractedField<T>(
                default,
                candidate.Confidence,
                candidate.IsRejected ? string.Join("; ", candidate.RejectReasons.Select(reason => reason.Message)) : lowConfidenceReason,
                candidate.Evidence,
                new[] { candidate });
        }

        return new ExtractedField<T>(
            candidate.Value,
            candidate.Confidence,
            string.Join("; ", candidate.Reasoning),
            candidate.Evidence,
            new[] { candidate });
    }
}
