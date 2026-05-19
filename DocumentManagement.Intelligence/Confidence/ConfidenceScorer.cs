using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;

namespace DocumentManagement.Intelligence.Confidence;

public sealed class ConfidenceScorer
{
    private readonly VietnameseTextNormalizer _normalizer;
    private readonly VietnameseOcrCorruptionDetector _ocrCorruptionDetector;

    public ConfidenceScorer(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
        _ocrCorruptionDetector = new VietnameseOcrCorruptionDetector(normalizer);
    }

    public ConfidenceScore Score(
        DocumentBlock block,
        DocumentStructure document,
        string expectedRegion,
        string[] keywords,
        double semanticConsistency,
        string expectedBlockType = "")
    {
        var spatial = SpatialContext.FromBlock(block, document);
        var corruption = _ocrCorruptionDetector.EstimateCorruption(block.RawText);
        if (LooksLikeAsciiVietnameseAdministrativeText(block.RawText, keywords))
        {
            corruption = Math.Max(corruption, 0.15);
        }

        var factors = new Dictionary<string, double>
        {
            ["layoutRegion"] = expectedRegion switch
            {
                "top-left" when spatial.IsTopLeft => 0.22,
                "top-right" when spatial.IsTopRight => 0.22,
                "bottom-left" when spatial.IsBottomLeft => 0.22,
                "bottom-right" when spatial.IsBottomRight => 0.22,
                "centered" when spatial.IsCentered => 0.16,
                _ => 0
            },
            ["blockType"] = string.IsNullOrWhiteSpace(expectedBlockType) ||
                block.BlockType.Equals(expectedBlockType, StringComparison.OrdinalIgnoreCase)
                    ? 0.12
                    : 0.04,
            ["keywordProximity"] = ContainsAny(block.NormalizedText, keywords) ? 0.22 : 0,
            ["ocrQuality"] = Math.Max(0, 0.18 - (corruption * 0.18)),
            ["semanticConsistency"] = Math.Clamp(semanticConsistency, 0, 0.25),
        };

        var reasoning = new List<string>
        {
            $"expected region: {expectedRegion}",
            $"block type: {block.BlockType}",
            $"semantic consistency: {semanticConsistency:0.00}"
        };

        var corruptionFindings = _ocrCorruptionDetector.Describe(block.RawText);
        if (LooksLikeAsciiVietnameseAdministrativeText(block.RawText, keywords) &&
            !corruptionFindings.Contains("Vietnamese text appears to be missing diacritics", StringComparer.Ordinal))
        {
            corruptionFindings = corruptionFindings.Append("Vietnamese text appears to be missing diacritics").ToArray();
        }

        if (corruptionFindings.Count > 0)
        {
            reasoning.Add("OCR findings: " + string.Join(", ", corruptionFindings));
        }

        return ConfidenceScore.FromFactors(factors, reasoning.ToArray());
    }

    private bool ContainsAny(string text, IEnumerable<string> keywords)
    {
        var haystack = _normalizer.ToSearchKey(text);
        return keywords.Any(keyword => haystack.Contains(_normalizer.ToSearchKey(keyword), StringComparison.Ordinal));
    }

    private bool LooksLikeAsciiVietnameseAdministrativeText(string text, IEnumerable<string> keywords)
    {
        if (text.Any(character => character > 127))
        {
            return false;
        }

        return ContainsAny(text, keywords) &&
            keywords.Any(keyword =>
                _normalizer.ToSearchKey(keyword) is "CONG TY" or "QUYET DINH" or "DIEU" or "NGAY" or "THANG" or "CHU TICH" or "GIAM DOC");
    }
}
