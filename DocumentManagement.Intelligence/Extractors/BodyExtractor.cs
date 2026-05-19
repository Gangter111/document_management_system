using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed partial class BodyExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;

    public BodyExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public IReadOnlyList<BodySection> Extract(DocumentStructure document)
    {
        var sections = new List<BodySection>();
        var blocks = document.ReadingOrderBlocks
            .Where(block => !IsHeaderOrFooter(block, document))
            .ToArray();

        for (var i = 0; i < blocks.Length; i++)
        {
            var block = blocks[i];
            if (block.NormalizedText.TrimStart().StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var match = ArticleRegex().Match(_normalizer.ToSearchKey(block.NormalizedText));
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number))
            {
                continue;
            }

            var confidence = _confidenceScorer.Score(
                block,
                document,
                "centered",
                new[] { "DIEU" },
                semanticConsistency: 0.24,
                expectedBlockType: "paragraph");
            var evidence = CreateSectionEvidence(blocks, i, document);
            var candidate = new ExtractionCandidate<string>(
                "Body section",
                block.NormalizedText,
                block.NormalizedText,
                confidence,
                confidence.Reasoning.Append("article marker found in reading-order body block").ToArray(),
                Array.Empty<RejectReason>(),
                evidence);

            sections.Add(new BodySection(
                number,
                $"Dieu {number}",
                string.Join(Environment.NewLine, evidence.Select(item => item.NormalizedText)),
                confidence,
                evidence,
                new[] { candidate }));
        }

        return sections;
    }

    private IReadOnlyList<BlockEvidence> CreateSectionEvidence(
        IReadOnlyList<DocumentBlock> blocks,
        int headingIndex,
        DocumentStructure document)
    {
        var heading = blocks[headingIndex];
        var evidence = new List<BlockEvidence>
        {
            BlockEvidence.FromBlock(
                heading,
                EvidenceRole.Primary,
                "BodyExtractor.article-marker",
                semanticRole: "body.section-heading")
        };

        var previous = heading;
        for (var i = headingIndex + 1; i < blocks.Count; i++)
        {
            var candidate = blocks[i];
            if (candidate.PageNumber != heading.PageNumber)
            {
                break;
            }

            if (ArticleRegex().IsMatch(_normalizer.ToSearchKey(candidate.NormalizedText)))
            {
                break;
            }

            var verticalGap = candidate.BoundingBox.Y1 - previous.BoundingBox.Y2;
            if (verticalGap < -2 || verticalGap > document.PageHeight * 0.08)
            {
                break;
            }

            if (candidate.BoundingBox.X1 < document.PageWidth * 0.08 ||
                candidate.BoundingBox.X1 > document.PageWidth * 0.85)
            {
                break;
            }

            evidence.Add(BlockEvidence.FromBlock(
                candidate,
                EvidenceRole.Contextual,
                "BodyExtractor.article-continuation",
                semanticRole: "body.section-paragraph"));
            previous = candidate;
        }

        return evidence;
    }

    private bool IsHeaderOrFooter(DocumentBlock block, DocumentStructure document)
    {
        if (block.IsTop(document.PageHeight) &&
            ExtractionText.ContainsAny(_normalizer, block.NormalizedText, "CONG HOA", "DOC LAP", "HANH PHUC", "CONG TY", "SO:"))
        {
            return true;
        }

        if (block.IsBottom(document.PageHeight) &&
            ExtractionText.ContainsAny(_normalizer, block.NormalizedText, "NOI NHAN", "LUU VT", "CHU TICH", "GIAM DOC"))
        {
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"\bDIEU\s+(?<number>[0-9]{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArticleRegex();
}
