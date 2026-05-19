using System.Text.RegularExpressions;
using DocumentManagement.Intelligence.Normalization;

namespace DocumentManagement.Intelligence.Confidence;

public sealed partial class VietnameseOcrCorruptionDetector
{
    private readonly VietnameseTextNormalizer _normalizer;

    public VietnameseOcrCorruptionDetector(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public double EstimateCorruption(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return 1;
        }

        var score = 0.0;
        if (BrokenUnicodeRegex().IsMatch(rawText))
        {
            score += 0.35;
        }

        if (NoiseRegex().IsMatch(rawText))
        {
            score += 0.2;
        }

        if (HasMixedCasing(rawText))
        {
            score += 0.15;
        }

        if (LooksVietnameseButMissingDiacritics(rawText))
        {
            score += 0.15;
        }

        return Math.Clamp(score, 0, 1);
    }

    public IReadOnlyList<string> Describe(string rawText)
    {
        var findings = new List<string>();
        if (BrokenUnicodeRegex().IsMatch(rawText))
        {
            findings.Add("broken unicode markers");
        }

        if (NoiseRegex().IsMatch(rawText))
        {
            findings.Add("OCR noise characters");
        }

        if (HasMixedCasing(rawText))
        {
            findings.Add("mixed casing");
        }

        if (LooksVietnameseButMissingDiacritics(rawText))
        {
            findings.Add("Vietnamese text appears to be missing diacritics");
        }

        return findings;
    }

    private bool LooksVietnameseButMissingDiacritics(string text)
    {
        var search = _normalizer.ToSearchKey(text);
        var hasVietnameseWords = search.Contains("CONG TY", StringComparison.Ordinal) ||
            search.Contains("QUYET DINH", StringComparison.Ordinal) ||
            search.Contains("DIEU", StringComparison.Ordinal) ||
            search.Contains("NGAY", StringComparison.Ordinal) ||
            search.Contains("THANG", StringComparison.Ordinal) ||
            search.Contains("CHU TICH", StringComparison.Ordinal) ||
            search.Contains("GIAM DOC", StringComparison.Ordinal);

        return hasVietnameseWords && !VietnameseDiacriticRegex().IsMatch(text);
    }

    private static bool HasMixedCasing(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length < 6)
        {
            return false;
        }

        var upper = letters.Count(char.IsUpper);
        var lower = letters.Count(char.IsLower);
        return upper > 0 && lower > 0 && Math.Min(upper, lower) / (double)letters.Length > 0.25;
    }

    [GeneratedRegex(@"Ã|Ä|Å|Æ|Ç|á»|áº|Â|�")]
    private static partial Regex BrokenUnicodeRegex();

    [GeneratedRegex(@"[|]{2,}|\.{2,}|[^\p{L}\p{N}\s.,:;()/\-]")]
    private static partial Regex NoiseRegex();

    [GeneratedRegex(@"[ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]", RegexOptions.IgnoreCase)]
    private static partial Regex VietnameseDiacriticRegex();
}
