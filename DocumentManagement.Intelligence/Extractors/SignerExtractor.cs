using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Extractors;

public sealed class SignerExtractor
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly ConfidenceScorer _confidenceScorer;
    private static readonly string[] SignerRoleKeywords =
    {
        "CHU TICH",
        "GIAM DOC",
        "TONG GIAM DOC",
        "PHO GIAM DOC",
        "TRUONG PHONG",
        "PHO TRUONG PHONG",
        "KT.",
        "TM.",
        "NGUOI KY",
        "DAI DIEN"
    };

    public SignerExtractor(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _confidenceScorer = new ConfidenceScorer(normalizer);
    }

    public ExtractedField<string> Extract(DocumentStructure document)
    {
        var candidates = new List<ExtractionCandidate<string>>();
        var roleBlocks = document.Blocks
            .Where(block => SpatialReasoning.IsBottomRight(block, document))
            .Where(block => ExtractionText.ContainsAny(_normalizer, block.NormalizedText, SignerRoleKeywords))
            .OrderBy(block => block.BoundingBox.Y1)
            .ToArray();

        foreach (var roleBlock in roleBlocks)
        {
            var nameBlock = SpatialReasoning.VerticalGroup(roleBlock, document, document.PageWidth * 0.22)
                .Where(block => block.BoundingBox.Y1 > roleBlock.BoundingBox.Y1)
                .FirstOrDefault(block => LooksLikePersonName(block.NormalizedText));

            if (nameBlock is null)
            {
                candidates.Add(ExtractionCandidate<string>.Rejected(
                    "Signer",
                    roleBlock.NormalizedText,
                    RejectReason.Soft("signer.missing-name", "signer role found without a nearby name block"),
                    new[]
                    {
                        BlockEvidence.FromBlock(
                            roleBlock,
                            extractionSource: "SignerExtractor.role-without-name",
                            rejectSource: "signer.missing-name",
                            semanticRole: "signer.role")
                    }));
                continue;
            }

            candidates.Add(CreateCandidate(roleBlock, nameBlock, document));
        }

        var ordered = candidates.OrderByDescending(candidate => candidate.Confidence.Value).ToArray();
        var best = ordered.FirstOrDefault(candidate => candidate.IsAccepted(0.6));
        if (best is null)
        {
            return new ExtractedField<string>(
                default,
                ConfidenceScore.Zero("No confident signer candidate found."),
                "No confident signer candidate found.",
                Array.Empty<BlockEvidence>(),
                ordered);
        }

        return new ExtractedField<string>(
            best.Value,
            best.Confidence,
            string.Join("; ", best.Reasoning),
            best.Evidence,
            ordered);
    }

    private ExtractionCandidate<string> CreateCandidate(DocumentBlock roleBlock, DocumentBlock nameBlock, DocumentStructure document)
    {
        var confidence = _confidenceScorer.Score(
            roleBlock,
            document,
            "bottom-right",
            SignerRoleKeywords,
            semanticConsistency: 0.22);
        var evidence = new[]
        {
            BlockEvidence.FromBlock(
                nameBlock,
                EvidenceRole.Primary,
                "SignerExtractor.name-below-role",
                semanticRole: "signer.name"),
            BlockEvidence.FromBlock(
                roleBlock,
                EvidenceRole.Supporting,
                "SignerExtractor.role-keyword",
                semanticRole: "signer.role")
        };

        return new ExtractionCandidate<string>(
            "Signer",
            nameBlock.NormalizedText,
            $"{roleBlock.NormalizedText} / {nameBlock.NormalizedText}",
            confidence,
            confidence.Reasoning.Append("person-like name found below signer role").ToArray(),
            Array.Empty<RejectReason>(),
            evidence);
    }

    private bool LooksLikePersonName(string text)
    {
        var search = _normalizer.ToSearchKey(text);
        if (ExtractionText.ContainsAny(_normalizer, text, "CONG TY", "CO PHAN", "NONG SAN", "PHU GIA", "NOI NHAN"))
        {
            return false;
        }

        if (ExtractionText.ContainsAny(_normalizer, text, SignerRoleKeywords))
        {
            return false;
        }

        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 2 and <= 5 && words.All(word => word.Length >= 2 && word.All(char.IsLetter));
    }
}
