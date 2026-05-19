using System.Text;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Confidence;

public sealed class ExtractionDebugReport
{
    public ExtractionDebugReport(
        IReadOnlyList<IExtractionCandidate> acceptedCandidates,
        IReadOnlyList<IExtractionCandidate> rejectedCandidates)
    {
        AcceptedCandidates = acceptedCandidates;
        RejectedCandidates = rejectedCandidates;
    }

    public IReadOnlyList<IExtractionCandidate> AcceptedCandidates { get; }

    public IReadOnlyList<IExtractionCandidate> RejectedCandidates { get; }

    public string ToConsoleText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== ACCEPTED ===");
        if (AcceptedCandidates.Count == 0)
        {
            builder.AppendLine("(empty)");
        }
        else
        {
            foreach (var candidate in AcceptedCandidates)
            {
                builder.AppendLine($"{candidate.FieldName}: {candidate.DisplayValue}");
                builder.AppendLine($"Confidence: {candidate.Confidence.Value:0.00}");
                builder.AppendLine("Reasoning:");
                foreach (var reason in candidate.Reasoning)
                {
                    builder.AppendLine($"- {reason}");
                }
                builder.AppendLine();
            }
        }

        builder.AppendLine("=== REJECTED ===");
        if (RejectedCandidates.Count == 0)
        {
            builder.AppendLine("(empty)");
        }
        else
        {
            foreach (var candidate in RejectedCandidates)
            {
                builder.AppendLine(candidate.CandidateText);
                builder.AppendLine("Reason:");
                foreach (var reason in candidate.RejectReasons)
                {
                    builder.AppendLine($"- {reason.Message}");
                }
                builder.AppendLine($"Confidence: {candidate.Confidence.Value:0.00}");
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}

public interface IExtractionCandidate
{
    string FieldName { get; }

    string CandidateText { get; }

    string DisplayValue { get; }

    ConfidenceScore Confidence { get; }

    IReadOnlyList<string> Reasoning { get; }

    IReadOnlyList<RejectReason> RejectReasons { get; }

    IReadOnlyList<BlockEvidence> Evidence { get; }
}
