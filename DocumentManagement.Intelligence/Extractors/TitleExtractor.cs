using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed partial class TitleExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;

    public TitleExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public ExtractedField<string> Extract(DocumentStructure document)
    {
        var candidates = document.Blocks
            .Where(block => IsTitleCandidate(block, document))
            .Select(block => CreateCandidate(block, document))
            .OrderByDescending(candidate => candidate.Confidence.Value)
            .ToArray();

        var best = candidates.FirstOrDefault(candidate => candidate.IsAccepted(0.6));
        if (best is null)
        {
            return new ExtractedField<string>(
                default,
                ConfidenceScore.Zero("No confident title candidate found."),
                "No confident title candidate found.",
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

    private ExtractionCandidate<string> CreateCandidate(DocumentBlock block, DocumentStructure document)
    {
        var evidence = new List<BlockEvidence>
        {
            BlockEvidence.FromBlock(
                block,
                EvidenceRole.Primary,
                "TitleExtractor.administrative-title",
                semanticRole: "title.document")
        };

        evidence.AddRange(CreateSupportingSubjectEvidence(block, document));
        var value = string.Join(Environment.NewLine, evidence.Select(item => item.NormalizedText));
        var confidence = _confidenceScorer.Score(
            block,
            document,
            "centered",
            new[] { "QUYET DINH", "THONG BAO", "CONG VAN", "TO TRINH", "BAO CAO", "KE HOACH", "NGHI QUYET" },
            semanticConsistency: HasSubjectText(block.NormalizedText) ? 0.24 : 0.18,
            expectedBlockType: block.BlockType);

        return new ExtractionCandidate<string>(
            "Title",
            value,
            block.NormalizedText,
            confidence,
            confidence.Reasoning.Append("Vietnamese administrative title marker found").ToArray(),
            Array.Empty<RejectReason>(),
            evidence);
    }

    private IEnumerable<BlockEvidence> CreateSupportingSubjectEvidence(DocumentBlock titleBlock, DocumentStructure document)
    {
        var maxGap = document.PageHeight * 0.08;
        return document.ReadingOrderBlocks
            .Where(block => block.PageNumber == titleBlock.PageNumber)
            .Where(block => block.ReadingOrder != titleBlock.ReadingOrder)
            .Where(block => block.BoundingBox.Y1 >= titleBlock.BoundingBox.Y2)
            .Where(block => block.BoundingBox.Y1 - titleBlock.BoundingBox.Y2 <= maxGap)
            .Where(block => SpatialReasoning.IsCentered(block, document))
            .Where(block => SubjectRegex().IsMatch(_normalizer.ToSearchKey(block.NormalizedText)))
            .OrderBy(block => block.BoundingBox.Y1)
            .ThenBy(block => block.BoundingBox.X1)
            .ThenBy(block => block.ReadingOrder)
            .Take(1)
            .Select(block => BlockEvidence.FromBlock(
                block,
                EvidenceRole.Supporting,
                "TitleExtractor.subject-continuation",
                semanticRole: "title.subject"));
    }

    private bool IsTitleCandidate(DocumentBlock block, DocumentStructure document)
    {
        if (block.BoundingBox.CenterY > Math.Max(1, document.PageHeight) * 0.45)
        {
            return false;
        }

        var search = _normalizer.ToSearchKey(block.NormalizedText);
        if (search.StartsWith("DIEU ", StringComparison.Ordinal) ||
            search.StartsWith("NOI NHAN", StringComparison.Ordinal) ||
            search.Contains("CONG HOA XA HOI", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TitleRegex().IsMatch(search))
        {
            return false;
        }

        return !TitleOnlyWithColonRegex().IsMatch(search) || HasNearbySubject(block, document);
    }

    private bool HasNearbySubject(DocumentBlock titleBlock, DocumentStructure document) =>
        CreateSupportingSubjectEvidence(titleBlock, document).Any();

    private static bool HasSubjectText(string text) =>
        text.Contains("V/v", StringComparison.OrdinalIgnoreCase) ||
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3;

    [GeneratedRegex(@"\b(QUYET\s+DINH|THONG\s+BAO|CONG\s+VAN|TO\s+TRINH|BAO\s+CAO|KE\s+HOACH|NGHI\s+QUYET)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^(QUYET\s+DINH|THONG\s+BAO|CONG\s+VAN|TO\s+TRINH|BAO\s+CAO|KE\s+HOACH|NGHI\s+QUYET)\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleOnlyWithColonRegex();

    [GeneratedRegex(@"^(V[/\\]V|VE\s+VIEC|VE\s+|TRICH\s+YEU)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectRegex();
}
