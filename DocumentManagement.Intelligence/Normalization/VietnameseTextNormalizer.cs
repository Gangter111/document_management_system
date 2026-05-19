using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentManagement.Intelligence.Normalization;

public sealed partial class VietnameseTextNormalizer
{
    private readonly Dictionary<string, string> _dictionaryReplacements = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> MojibakeMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Ã¡"] = "a", ["Ã "] = "a", ["Ã¢"] = "a", ["Ã£"] = "a", ["Ã¨"] = "e",
        ["Ã©"] = "e", ["Ãª"] = "e", ["Ã¬"] = "i", ["Ã­"] = "i", ["Ã²"] = "o",
        ["Ã³"] = "o", ["Ã´"] = "o", ["Ãµ"] = "o", ["Ã¹"] = "u", ["Ãº"] = "u",
        ["Ã½"] = "y", ["Ä‘"] = "d", ["Ä"] = "D", ["Äƒ"] = "a", ["Ä‚"] = "A",
        ["Ä›"] = "e", ["Ä“"] = "e", ["Ä©"] = "i", ["Ä"] = "a", ["Å©"] = "u",
        ["Å"] = "o", ["Æ°"] = "u", ["Æ¡"] = "o", ["ÇŽ"] = "a",
        ["á»"] = "e", ["á»‡"] = "e", ["á»±"] = "u", ["á»‘"] = "o", ["á»“"] = "o",
        ["á»©"] = "u", ["á»«"] = "u", ["á»Ÿ"] = "o", ["á»™"] = "o", ["áº£"] = "a",
        ["áº¥"] = "a", ["áº§"] = "a", ["áº¡"] = "a", ["á»‹"] = "i",
    };

    public VietnameseTextNormalizer()
    {
        AddDictionaryEntries(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ToNG"] = "TONG",
            ["GIAm"] = "GIAM",
            ["Co PHAN"] = "CO PHAN",
            ["s6:"] = "So:",
            ["DiÃ¨u"] = "Dieu",
            ["Dieu"] = "Dieu",
            ["CÇŽn cir"] = "Can cu",
            ["nÇŽm"] = "nam",
            ["ngÃ y"] = "ngay",
            ["thÃ¡ng"] = "thang",
            ["miÄ›n"] = "mien",
            ["NguyÄ›n"] = "Nguyen",
        });
    }

    public void AddDictionaryEntry(string source, string replacement)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            _dictionaryReplacements[source] = replacement;
        }
    }

    public void AddDictionaryEntries(IReadOnlyDictionary<string, string> replacements)
    {
        foreach (var replacement in replacements)
        {
            AddDictionaryEntry(replacement.Key, replacement.Value);
        }
    }

    public string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim();
        foreach (var pair in MojibakeMap)
        {
            normalized = normalized.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        normalized = normalized
            .Replace('Ð', 'D')
            .Replace('ð', 'd')
            .Replace('ǎ', 'a')
            .Replace('Ǎ', 'A');

        foreach (var replacement in _dictionaryReplacements)
        {
            normalized = normalized.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
        }

        normalized = WhitespaceRegex().Replace(normalized, " ");
        return normalized.Trim();
    }

    public string ToSearchKey(string? text)
    {
        var normalized = Normalize(text).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(character));
            }
        }

        return WhitespaceRegex().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
