using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed partial class RecipientExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;

    public RecipientExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public ExtractedField<string> Extract(DocumentStructure document)
    {
        var candidates = document.ReadingOrderBlocks
            .Where(block => IsRecipientHeading(block, document))
            .Select(block => CreateCandidate(block, document))
            .OrderByDescending(candidate => IsKinhGuiCandidate(candidate) ? 1 : 0)
            .ThenByDescending(candidate => candidate.Confidence.Value)
            .ToArray();

        var best = candidates.FirstOrDefault(candidate => candidate.IsAccepted(0.6));
        if (best is null)
        {
            return new ExtractedField<string>(
                default,
                ConfidenceScore.Zero("No confident recipient candidate found."),
                "No confident recipient candidate found.",
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

    private ExtractionCandidate<string> CreateCandidate(DocumentBlock heading, DocumentStructure document)
    {
        var evidence = new List<BlockEvidence>
        {
            BlockEvidence.FromBlock(
                heading,
                EvidenceRole.Primary,
                "RecipientExtractor.recipient-heading",
                semanticRole: "recipient.heading")
        };
        evidence.AddRange(CreateRecipientEntries(heading, document));

        var value = string.Join(Environment.NewLine, evidence.Select(item => item.NormalizedText));
        var confidence = _confidenceScorer.Score(
            heading,
            document,
            SpatialReasoning.IsBottomLeft(heading, document) ? "bottom-left" : "centered",
            new[] { "NOI NHAN", "KINH GUI" },
            semanticConsistency: evidence.Count > 1 ? 0.24 : 0.16,
            expectedBlockType: heading.BlockType);

        return new ExtractionCandidate<string>(
            "Recipients",
            value,
            heading.NormalizedText,
            confidence,
            confidence.Reasoning.Append("recipient heading and ordered entries found").ToArray(),
            Array.Empty<RejectReason>(),
            evidence);
    }

    private IEnumerable<BlockEvidence> CreateRecipientEntries(DocumentBlock heading, DocumentStructure document)
    {
        var previous = heading;
        foreach (var block in document.ReadingOrderBlocks.Where(block => block.PageNumber == heading.PageNumber && block.BoundingBox.Y1 > heading.BoundingBox.Y1))
        {
            if (block.BoundingBox.Y1 - previous.BoundingBox.Y2 > document.PageHeight * 0.08)
            {
                break;
            }

            if (block.BoundingBox.X1 > document.PageWidth * 0.42)
            {
                continue;
            }

            var search = _normalizer.ToSearchKey(block.NormalizedText);
            if (!RecipientEntryRegex().IsMatch(search))
            {
                break;
            }

            yield return BlockEvidence.FromBlock(
                block,
                EvidenceRole.Contextual,
                "RecipientExtractor.recipient-entry",
                semanticRole: "recipient.entry");
            previous = block;
        }
    }

    private bool IsRecipientHeading(DocumentBlock block, DocumentStructure document)
    {
        var search = _normalizer.ToSearchKey(block.NormalizedText);
        if (!RecipientHeadingRegex().IsMatch(search))
        {
            return false;
        }

        if (SpatialReasoning.IsBottomLeft(block, document) ||
            (block.BoundingBox.CenterY <= Math.Max(1, document.PageHeight) * 0.45 && SpatialReasoning.IsCentered(block, document)))
        {
            return true;
        }

        return search.Contains("KINH GUI", StringComparison.Ordinal) &&
            block.BoundingBox.CenterY <= Math.Max(1, document.PageHeight) * 0.55 &&
            block.BoundingBox.X1 <= Math.Max(1, document.PageWidth) * 0.35;
    }

    private bool IsKinhGuiCandidate(ExtractionCandidate<string> candidate) =>
        ExtractionText.ContainsAny(_normalizer, candidate.CandidateText, "KINH GUI");

    [GeneratedRegex(@"\b(NOI\s+NHAN|KINH\s+GUI)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecipientHeadingRegex();

    [GeneratedRegex(@"^(-|Nhu\b|Luu\b|Kinh\s+gui\b|Ong\b|Ba\b|Cac\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecipientEntryRegex();
}
