using DocumentManagement.Intelligence.Confidence;

namespace DocumentManagement.Intelligence.Results;

public sealed record BodySection(
    int Number,
    string Heading,
    string Text,
    ConfidenceScore ConfidenceScore,
    IReadOnlyList<BlockEvidence> Evidence,
    IReadOnlyList<ExtractionCandidate<string>> Candidates)
{
    public double Confidence => ConfidenceScore.Value;
}
