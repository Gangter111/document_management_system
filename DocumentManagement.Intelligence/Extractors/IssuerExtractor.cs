using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed partial class IssuerExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;
    private static readonly string[] IssuerKeywords =
    {
        "CONG TY",
        "CTY",
        "UBND",
        "HDND",
        "VAN PHONG",
        "SO",
        "BO"
    };

    public IssuerExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public ExtractedField<string> Extract(DocumentStructure document)
    {
        var candidates = document.Blocks
            .Where(block => ExtractionText.ContainsAny(_normalizer, block.NormalizedText, IssuerKeywords))
            .Where(block => IsIssuerCandidate(block, document))
            .Select(block => CreateCandidate(block, document))
            .OrderByDescending(candidate => candidate.Confidence.Value)
            .ToArray();

        var best = candidates.FirstOrDefault(candidate => candidate.IsAccepted(0.6));
        if (best is null)
        {
            return new ExtractedField<string>(
                default,
                ConfidenceScore.Zero("No confident top-left issuer candidate found."),
                "No confident top-left issuer candidate found.",
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
        var issuer = ExtractIssuerText(block.NormalizedText);
        issuer = NormalizeIssuerSpacing(issuer);
        var evidence = CreateEvidence(block, document);

        if (string.IsNullOrWhiteSpace(issuer))
        {
            return ExtractionCandidate<string>.Rejected(
                "Issuer",
                block.NormalizedText,
                RejectReason.Soft("issuer.empty", "issuer candidate became empty after removing document number"),
                evidence);
        }

        var confidence = _confidenceScorer.Score(
            block,
            document,
            SpatialReasoning.IsTopLeft(block, document) ? "top-left" : "centered",
            IssuerKeywords,
            semanticConsistency: issuer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2 ? 0.22 : 0.08,
            expectedBlockType: "page_header");

        return new ExtractionCandidate<string>(
            "Issuer",
            issuer,
            block.NormalizedText,
            confidence,
            confidence.Reasoning.Append(SpatialReasoning.IsTopLeft(block, document)
                ? "issuer keyword found in administrative header"
                : "issuer organization line found near document opening").ToArray(),
            Array.Empty<RejectReason>(),
            evidence);
    }

    private bool IsIssuerCandidate(DocumentBlock block, DocumentStructure document)
    {
        var search = _normalizer.ToSearchKey(block.NormalizedText);
        if (search.Contains("CONG HOA XA HOI", StringComparison.Ordinal) ||
            search.StartsWith("DIEU ", StringComparison.Ordinal) ||
            search.StartsWith("QUYET DINH", StringComparison.Ordinal) ||
            search.StartsWith("NOI NHAN", StringComparison.Ordinal) ||
            search.Contains("CHU TICH", StringComparison.Ordinal) ||
            search.Contains("GIAM DOC", StringComparison.Ordinal) && !search.Contains("CONG TY", StringComparison.Ordinal))
        {
            return false;
        }

        if (SpatialReasoning.IsTopLeft(block, document))
        {
            return !search.Contains("VAN PHONG", StringComparison.Ordinal) ||
                HasAdministrativeOrganizationAnchor(search);
        }

        if (block.BoundingBox.CenterY > Math.Max(1, document.PageHeight) * 0.42)
        {
            return false;
        }

        return OrganizationStartRegex().IsMatch(search) && HasAdministrativeOrganizationAnchor(search);
    }

    private static string ExtractIssuerText(string text)
    {
        var withoutNumber = DocumentNumberTailRegex().Replace(text, string.Empty).Trim(' ', '-', ',');
        var match = OrganizationStartRegex().Match(withoutNumber);
        return match.Success ? withoutNumber[match.Index..].Trim(' ', '-', ',') : withoutNumber;
    }

    private static bool HasAdministrativeOrganizationAnchor(string search) =>
        search.Contains("CONG TY", StringComparison.Ordinal) ||
        search.Contains("CTY", StringComparison.Ordinal) ||
        search.Contains("UBND", StringComparison.Ordinal) ||
        search.Contains("HDND", StringComparison.Ordinal) ||
        search.Contains("SO ", StringComparison.Ordinal) ||
        search.Contains("BO ", StringComparison.Ordinal);

    private static string NormalizeIssuerSpacing(string issuer) =>
        issuer
            .Replace("CONGTY", "CONG TY", StringComparison.OrdinalIgnoreCase)
            .Replace("COPHAN", "CO PHAN", StringComparison.OrdinalIgnoreCase)
            .Replace("NONGSAN", "NONG SAN", StringComparison.OrdinalIgnoreCase)
            .Replace("PHUGIA", "PHU GIA", StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<BlockEvidence> CreateEvidence(DocumentBlock primaryBlock, DocumentStructure document)
    {
        var support = document.ReadingOrderBlocks
            .Where(block => block.PageNumber == primaryBlock.PageNumber)
            .Where(block => !ReferenceEquals(block, primaryBlock))
            .Where(block => SpatialReasoning.IsTopLeft(block, document))
            .Where(block => Math.Abs(block.BoundingBox.X1 - primaryBlock.BoundingBox.X1) <= document.PageWidth * 0.08)
            .Where(block => Math.Abs(block.BoundingBox.CenterY - primaryBlock.BoundingBox.CenterY) <= document.PageHeight * 0.08)
            .Where(block => !ExtractionText.ContainsAny(_normalizer, block.NormalizedText, "CONG HOA", "DOC LAP", "HANH PHUC"))
            .OrderBy(block => block.BoundingBox.Y1)
            .ThenBy(block => block.BoundingBox.X1)
            .ThenBy(block => block.ReadingOrder)
            .Select(block => BlockEvidence.FromBlock(
                block,
                EvidenceRole.Supporting,
                "IssuerExtractor.top-left-header-continuation",
                semanticRole: "issuer.header-line"))
            .ToArray();

        return new[]
        {
            BlockEvidence.FromBlock(
                primaryBlock,
                EvidenceRole.Primary,
                "IssuerExtractor.top-left-organization-keyword",
                semanticRole: "issuer.name")
        }.Concat(support).ToArray();
    }

    [GeneratedRegex(@"\b(S\s*[O06]|SO|S6|S0)\s*[:.]?\s*.*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentNumberTailRegex();

    [GeneratedRegex(@"\b(CONG\s*TY|CTY|UBND|HDND|VAN\s+PHONG|SO\s+[A-Z]|BO\s+[A-Z])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrganizationStartRegex();

}
