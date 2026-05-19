using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed partial class IssueDateExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;

    public IssueDateExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public ExtractedField<DateOnly> Extract(DocumentStructure document)
    {
        var candidates = new List<ExtractionCandidate<DateOnly>>();
        foreach (var block in document.Blocks)
        {
            var normalized = block.NormalizedText;
            var searchable = _normalizer.ToSearchKey(normalized);
            if (IsRejectedDateContext(searchable))
            {
                candidates.AddRange(CreateHardRejectedDobCandidates(block, searchable));
                continue;
            }

            var match = VietnameseDateRegex().Match(searchable);
            if (match.Success)
            {
                candidates.Add(CreateVietnameseDateCandidate(block, match, document));
            }
        }

        var ordered = candidates.OrderByDescending(candidate => candidate.Confidence.Value).ToArray();
        var best = ordered.FirstOrDefault(candidate => candidate.IsAccepted(0.65));
        if (best is null)
        {
            return new ExtractedField<DateOnly>(
                default,
                ConfidenceScore.Zero("No confident issue-date candidate found."),
                "No confident issue-date candidate found.",
                Array.Empty<BlockEvidence>(),
                ordered);
        }

        return new ExtractedField<DateOnly>(
            best.Value,
            best.Confidence,
            string.Join("; ", best.Reasoning),
            best.Evidence,
            ordered);
    }

    private IEnumerable<ExtractionCandidate<DateOnly>> CreateHardRejectedDobCandidates(DocumentBlock block, string searchableText)
    {
        var evidence = new[]
        {
            BlockEvidence.FromBlock(
                block,
                extractionSource: "IssueDateExtractor.date-context",
                rejectSource: "date.dob-context",
                semanticRole: "date.rejected-dob-context")
        };
        var matches = NumericDateRegex().Matches(searchableText)
            .Select(match => match.Value)
            .Concat(VietnameseDateRegex().Matches(searchableText).Select(ToDisplayDateText))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            matches = new[] { block.NormalizedText };
        }

        foreach (var candidateText in matches)
        {
            yield return ExtractionCandidate<DateOnly>.Rejected(
                "Issue date",
                candidateText,
                RejectReason.Hard("date.dob-context", "contains DOB context"),
                evidence);
        }
    }

    private ExtractionCandidate<DateOnly> CreateVietnameseDateCandidate(DocumentBlock block, Match match, DocumentStructure document)
    {
        var evidence = new[]
        {
            BlockEvidence.FromBlock(
                block,
                extractionSource: "IssueDateExtractor.vietnamese-date",
                semanticRole: "document.issue-date")
        };
        if (!TryReadStrictNumber(match.Groups["day"].Value, 1, 31, out var day) ||
            !TryReadStrictNumber(match.Groups["month"].Value, 1, 12, out var month) ||
            !int.TryParse(match.Groups["year"].Value, out var year) ||
            year < 1900 ||
            year > 2100 ||
            !DateOnly.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", out var date))
        {
            return ExtractionCandidate<DateOnly>.Rejected(
                "Issue date",
                ToDisplayDateText(match),
                RejectReason.Soft("date.uncertain-ocr", "date token contains uncertain OCR; no silent recovery"),
                evidence
                    .Select(item => item with { RejectSource = "date.uncertain-ocr" })
                    .ToArray());
        }

        var confidence = _confidenceScorer.Score(
            block,
            document,
            "top-right",
            new[] { "ngay", "thang", "nam" },
            semanticConsistency: 0.22,
            expectedBlockType: "page_header");

        return new ExtractionCandidate<DateOnly>(
            "Issue date",
            date,
            ToDisplayDateText(match),
            confidence,
            confidence.Reasoning.Append("valid issue-date pattern found outside DOB context").ToArray(),
            Array.Empty<RejectReason>(),
            evidence);
    }

    private static bool IsRejectedDateContext(string key)
    {
        return key.Contains("SINH NGAY", StringComparison.Ordinal) ||
            key.Contains("NGAY SINH", StringComparison.Ordinal) ||
            key.Contains("DOB", StringComparison.Ordinal);
    }

    private static bool TryReadStrictNumber(string token, int min, int max, out int number)
    {
        number = 0;
        if (!StrictDigitsRegex().IsMatch(token))
        {
            return false;
        }

        return int.TryParse(token, out number) && number >= min && number <= max;
    }

    private static string ToDisplayDateText(Match match)
    {
        var day = match.Groups["day"].Value;
        var month = match.Groups["month"].Value;
        var year = match.Groups["year"].Value;
        return $"{day}/{month}/{year}";
    }

    [GeneratedRegex(@"(?<day>[0-9]{1,2})[/-](?<month>[0-9]{1,2})[/-](?<year>[12][0-9]{3})", RegexOptions.CultureInvariant)]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(@"ngay\s*\.{0,3}(?<day>[0-9A-Za-z|]{1,4})\.{0,3}\s*thang\s*\.{0,3}(?<month>[0-9A-Za-z|]{1,3})\.{0,3}\s*nam\s*(?<year>[12][0-9]{3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VietnameseDateRegex();

    [GeneratedRegex(@"^[0-9]{1,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex StrictDigitsRegex();
}
