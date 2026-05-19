using DocumentManagement.Intelligence.Normalization;

namespace DocumentManagement.Intelligence.Extractors;

internal static class ExtractionText
{
    public static bool ContainsAny(VietnameseTextNormalizer normalizer, string text, params string[] needles)
    {
        var key = normalizer.ToSearchKey(text);
        return needles.Any(needle => key.Contains(normalizer.ToSearchKey(needle), StringComparison.Ordinal));
    }
}
