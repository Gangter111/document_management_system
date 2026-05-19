using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Confidence;

public sealed class ExtractionDebugReportGenerator
{
    public ExtractionDebugReport Generate(DocumentExtractionResult result)
    {
        var scalarFields = new IFieldDebugSource[]
        {
            new FieldDebugSource<string>(result.Issuer),
            new FieldDebugSource<string>(result.DocumentNumber),
            new FieldDebugSource<DateOnly>(result.IssueDate),
            new FieldDebugSource<string>(result.Title),
            new FieldDebugSource<string>(result.Signer),
            new FieldDebugSource<string>(result.Recipients),
        };

        var acceptedCandidates = new List<IExtractionCandidate>();
        var rejectedCandidates = new List<IExtractionCandidate>();

        foreach (var field in scalarFields)
        {
            if (field.AcceptedCandidate is not null)
            {
                acceptedCandidates.Add(field.AcceptedCandidate);
            }

            rejectedCandidates.AddRange(field.RejectedCandidates);
        }

        acceptedCandidates.AddRange(result.BodySections.SelectMany(section => section.Candidates));

        var accepted = acceptedCandidates
            .OrderBy(candidate => candidate.FieldName)
            .ThenByDescending(candidate => candidate.Confidence.Value)
            .ToArray();

        var rejected = rejectedCandidates
            .OrderBy(candidate => candidate.FieldName)
            .ThenBy(candidate => candidate.CandidateText)
            .ToArray();

        return new ExtractionDebugReport(accepted, rejected);
    }

    private interface IFieldDebugSource
    {
        IExtractionCandidate? AcceptedCandidate { get; }

        IReadOnlyList<IExtractionCandidate> RejectedCandidates { get; }
    }

    private sealed class FieldDebugSource<T> : IFieldDebugSource
    {
        private readonly ExtractedField<T> _field;

        public FieldDebugSource(ExtractedField<T> field)
        {
            _field = field;
        }

        public IExtractionCandidate? AcceptedCandidate =>
            _field.HasValue
                ? _field.Candidates.FirstOrDefault(candidate =>
                    candidate.RejectReasons.Count == 0 &&
                    candidate.Confidence.Value > 0 &&
                    string.Equals(candidate.DisplayValue, FormatValue(_field.Value), StringComparison.Ordinal))
                : null;

        public IReadOnlyList<IExtractionCandidate> RejectedCandidates =>
            _field.Candidates
                .Where(candidate => candidate.RejectReasons.Count > 0 || candidate.Confidence.Value <= 0)
                .ToArray();

        private static string FormatValue(T? value) =>
            value is DateOnly date ? date.ToString("yyyy-MM-dd") : value?.ToString() ?? string.Empty;
    }
}
