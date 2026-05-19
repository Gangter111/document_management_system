using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed partial class DocumentNumberExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;

    public DocumentNumberExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public ExtractedField<string> Extract(DocumentStructure document)
    {
        var candidates = document.Blocks
            .Select(block => (Block: block, Match: NumberRegex().Match(_normalizer.ToSearchKey(block.NormalizedText))))
            .Where(candidate => candidate.Match.Success)
            .Select(candidate => CreateCandidate(candidate.Block, candidate.Match, document))
            .OrderByDescending(candidate => candidate.Confidence.Value)
            .ToArray();

        var best = candidates.FirstOrDefault(candidate => candidate.IsAccepted(0.6));
        if (best is null)
        {
            return new ExtractedField<string>(
                default,
                ConfidenceScore.Zero("No confident document-number candidate found."),
                "No confident document-number candidate found.",
                Array.Empty<BlockEvidence>(),
                candidates);
        }

        return new ExtractedField<string>(
            best.Value,
            best.Confidence,
            string.Join("; ", best.Reasoning),
            best.Evidence,
            candidates);
    }

    private ExtractionCandidate<string> CreateCandidate(DocumentBlock block, Match match, DocumentStructure document)
    {
        var value = NormalizeDocumentNumber(match.Groups["number"].Value);
        var evidence = new[]
        {
            BlockEvidence.FromBlock(
                block,
                extractionSource: "DocumentNumberExtractor.number-marker",
                semanticRole: "document.number")
        };
        var semanticConsistency = value.Contains('/', StringComparison.Ordinal) ? 0.22 : 0.1;
        var confidence = _confidenceScorer.Score(
            block,
            document,
            "top-left",
            new[] { "SO", "S6", "S0", "NO" },
            semanticConsistency,
            expectedBlockType: "page_header");

        return new ExtractionCandidate<string>(
            "Document number",
            value,
            block.NormalizedText,
            confidence,
            confidence.Reasoning.Append("document number marker and value found").ToArray(),
            Array.Empty<RejectReason>(),
            evidence);
    }

    private static string NormalizeDocumentNumber(string value) =>
        WhitespaceRegex()
            .Replace(value.Trim().TrimEnd('.', ',', ';'), string.Empty)
            .Replace('Đ', 'D')
            .Replace('Ð', 'D');

    [GeneratedRegex(@"\b(?:S\s*[O06]|SO|S6|S0|NO|KY\s*HIEU)\b\s*[:.]?\s*(?<number>[0-9A-ZĐD][0-9A-ZĐD./\-\s]{2,}?)(?=\s+(?:NGAY|THANG|NAM|VE|V[/\\]V|TRICH\s+YEU)\b|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
