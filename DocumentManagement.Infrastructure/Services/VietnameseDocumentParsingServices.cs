using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;

namespace DocumentManagement.Infrastructure.Services;

internal static class VietnameseOcrLexicon
{
    public static readonly IReadOnlyDictionary<string, string[]> DocumentKinds =
        new Dictionary<string, string[]>
        {
            ["QUYET_DINH"] = ["QUYET DINH", "QD", "DQ", "QO", "Q0", "OO"],
            ["CONG_VAN"] = ["CONG VAN"],
            ["THONG_BAO"] = ["THONG BAO"],
            ["TO_TRINH"] = ["TO TRINH"],
            ["BIEN_BAN"] = ["BIEN BAN"]
        };

    public static readonly string[] NationalHeaderTokens = ["CONG HOA XA HOI CHU NGHIA VIET NAM", "DOC LAP - TU DO - HANH PHUC"];
    public static readonly string[] HeaderOrganizations = ["CONG TY", "UBND", "SO ", "BO ", "PHONG ", "BAN "];
    public static readonly string[] SignatureTitles = ["KT.", "TM.", "GIAM DOC", "CHU TICH", "PHO GIAM DOC"];
    public static readonly string[] IssueDateRejectors = ["SINH NGAY", "NGAY SINH", "CMND", "CCCD"];
}

public sealed class VietnameseOcrNormalizationService : IOcrNormalizationService
{
    public string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = NormalizeForDisplay(value)
            .Replace("QĐ", "QD", StringComparison.OrdinalIgnoreCase)
            .Replace("DQ", "QD", StringComparison.OrdinalIgnoreCase)
            .Replace("QO", "QD", StringComparison.OrdinalIgnoreCase)
            .Replace("Q0", "QD", StringComparison.OrdinalIgnoreCase)
            .Replace("OO", "QD", StringComparison.OrdinalIgnoreCase);

        return RemoveDiacritics(normalized).ToUpperInvariant();
    }

    public string NormalizeForDisplay(string value)
    {
        return Regex.Replace(
                value.Normalize(NormalizationForm.FormC)
                    .Replace('–', '-')
                    .Replace('—', '-')
                    .Replace('“', '"')
                    .Replace('”', '"'),
                @"\s+",
                " ")
            .Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

public sealed class VietnameseTextBlockSegmenter : ITextBlockSegmenter
{
    public IReadOnlyList<TextBlock> Segment(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count == 0)
            return Array.Empty<TextBlock>();

        var groups = new List<List<OcrLine>>();
        foreach (var line in lines.OrderBy(line => line.PageIndex).ThenBy(line => line.BoundingBox.Y))
        {
            var current = groups.LastOrDefault();
            var previous = current?.LastOrDefault();
            var verticalGap = previous == null ? int.MaxValue : line.BoundingBox.Y - (previous.BoundingBox.Y + previous.BoundingBox.Height);
            if (previous == null || previous.PageIndex != line.PageIndex || verticalGap > Math.Max(previous.BoundingBox.Height, 18))
                groups.Add([line]);
            else
                current!.Add(line);
        }

        return groups.Select((group, index) =>
        {
            var text = string.Join(' ', group.Select(line => line.Text));
            var normalized = string.Join(' ', group.Select(line => line.NormalizedText));
            var box = Union(group.Select(line => line.BoundingBox));
            return new TextBlock(
                index,
                group[0].PageIndex,
                text,
                normalized,
                box,
                group.Average(line => line.Confidence),
                SectionType.Unknown,
                BlockRole.Unknown,
                group);
        }).ToList();
    }

    private static BoundingBox Union(IEnumerable<BoundingBox> boxes)
    {
        var array = boxes.ToArray();
        var left = array.Min(box => box.X);
        var top = array.Min(box => box.Y);
        var right = array.Max(box => box.X + box.Width);
        var bottom = array.Max(box => box.Y + box.Height);
        return new BoundingBox(left, top, right - left, bottom - top);
    }
}

public sealed class VietnameseLayoutAnalyzer : ILayoutAnalyzer
{
    private readonly ITextBlockSegmenter _segmenter;

    public VietnameseLayoutAnalyzer(ITextBlockSegmenter? segmenter = null)
    {
        _segmenter = segmenter ?? new VietnameseTextBlockSegmenter();
    }

    public DocumentLayout Analyze(OcrDocument ocrDocument)
    {
        var blocks = _segmenter.Segment(ocrDocument.Lines)
            .Select(ClassifyBlock)
            .ToList();

        return new DocumentLayout(
            ocrDocument.RawText,
            string.Join('\n', ocrDocument.Lines.Select(line => line.NormalizedText)),
            ocrDocument.Lines,
            blocks);
    }

    private static TextBlock ClassifyBlock(TextBlock block)
    {
        var y = block.BoundingBox.Y;
        var upperRatio = block.Text.Count(char.IsUpper) / (double)Math.Max(1, block.Text.Count(char.IsLetter));
        var normalized = block.NormalizedText;

        if (VietnameseOcrLexicon.NationalHeaderTokens.Any(normalized.Contains))
            return block with { SectionType = SectionType.NationalHeader, Role = BlockRole.NationalMotto };
        if (VietnameseOcrLexicon.HeaderOrganizations.Any(normalized.Contains) && y < 400)
            return block with { SectionType = SectionType.Header, Role = BlockRole.Organization };
        if (normalized.Contains("NOI NHAN"))
            return block with { SectionType = SectionType.Recipient, Role = BlockRole.RecipientList };
        if (VietnameseOcrLexicon.SignatureTitles.Any(normalized.Contains) && y > 500)
            return block with { SectionType = SectionType.Signature, Role = BlockRole.SignatureTitle };
        if (normalized.Contains("CAN CU"))
            return block with { SectionType = SectionType.LegalBasis, Role = BlockRole.Paragraph };
        if (normalized.StartsWith("DIEU "))
            return block with { SectionType = SectionType.Articles, Role = BlockRole.Paragraph };
        if (upperRatio > 0.75 && y < 550)
            return block with { SectionType = SectionType.Title, Role = BlockRole.Title };
        if (y > 1000)
            return block with { SectionType = SectionType.Footer, Role = BlockRole.Paragraph };
        return block with { SectionType = SectionType.Body, Role = BlockRole.Paragraph };
    }
}

public sealed class VietnameseAdministrativeDocumentClassifier : IDocumentClassifier
{
    public DocumentClassification Classify(DocumentLayout layout)
    {
        var scores = VietnameseOcrLexicon.DocumentKinds.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Sum(token => layout.Blocks.Take(8).Count(block => block.NormalizedText.Contains(token, StringComparison.Ordinal))) * 0.35);
        var winner = scores.OrderByDescending(pair => pair.Value).FirstOrDefault();
        return new DocumentClassification(winner.Value > 0 ? winner.Key : "UNKNOWN", Math.Clamp(winner.Value, 0, 1), scores);
    }
}

public sealed class VietnameseAdministrativeFieldExtractor : IFieldExtractor
{
    private static readonly Regex NumberRegex = new(@"^\s*(?:SO|S0)\s*[:\-]\s*([A-Z0-9\/\-.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VerboseDateRegex = new(@"NGAY\s+(\d{1,2})\s+THANG\s+(\d{1,2})\s+NAM\s+(\d{4})", RegexOptions.Compiled);
    private static readonly Regex NumericDateRegex = new(@"\b(\d{1,2})[\/\-.](\d{1,2})[\/\-.](\d{4})\b", RegexOptions.Compiled);

    public IReadOnlyList<ExtractedFieldCandidate> ExtractCandidates(DocumentLayout layout, DocumentClassification classification)
    {
        var candidates = new List<ExtractedFieldCandidate>();
        foreach (var line in layout.Lines)
        {
            AddDocumentNumber(line, layout, candidates);
            AddIssueDates(line, layout, candidates);
            AddIssuer(line, layout, candidates);
            AddRecipient(line, layout, candidates);
            AddSigner(line, layout, candidates);
        }

        AddTitle(layout, candidates);
        AddBody(layout, candidates);
        return candidates;
    }

    private static void AddDocumentNumber(OcrLine line, DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        var match = NumberRegex.Match(line.NormalizedText);
        if (match.Success)
            candidates.Add(Create("DocumentNumber", match.Groups[1].Value, line, layout, "number-label", 35, ["label:so"], []));
    }

    private static void AddIssueDates(OcrLine line, DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        foreach (Match match in VerboseDateRegex.Matches(line.NormalizedText).Cast<Match>().Concat(NumericDateRegex.Matches(line.NormalizedText).Cast<Match>()))
        {
            var isVerbose = match.Groups.Count == 4 && line.NormalizedText.Contains("THANG");
            var value = $"{match.Groups[3].Value}-{match.Groups[2].Value.PadLeft(2, '0')}-{match.Groups[1].Value.PadLeft(2, '0')}";
            var rejects = VietnameseOcrLexicon.IssueDateRejectors.Where(line.NormalizedText.Contains).ToList();
            var signals = new List<string>();
            if (line.NormalizedText.Contains("KY NGAY"))
                signals.Add("contains:ky-ngay");
            if (GetSection(layout, line.Index) is SectionType.Header or SectionType.NationalHeader)
                signals.Add("region:header");
            if (isVerbose)
                signals.Add("format:official-date");
            candidates.Add(Create("IssueDate", value, line, layout, isVerbose ? "official-date" : "numeric-date", isVerbose ? 30 : 10, signals, rejects));
        }
    }

    private static void AddIssuer(OcrLine line, DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        if (line.NormalizedText.Contains("CO QUAN BAN HANH") || line.NormalizedText.Contains("NOI BAN HANH"))
        {
            candidates.Add(Create("SenderName", ValueAfterSeparator(line.Text), line, layout, "issuer-label", 45, ["label:issuer"], []));
        }
        else if (GetSection(layout, line.Index) == SectionType.Header &&
            VietnameseOcrLexicon.HeaderOrganizations.Any(line.NormalizedText.Contains) &&
            !VietnameseOcrLexicon.NationalHeaderTokens.Any(line.NormalizedText.Contains))
        {
            candidates.Add(Create("SenderName", line.Text, line, layout, "header-organization", 30, ["region:header", "org-token"], []));
        }
    }

    private static void AddRecipient(OcrLine line, DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        if (line.NormalizedText.Contains("KINH GUI") || line.NormalizedText.StartsWith("NOI NHAN"))
        {
            var value = ValueAfterSeparator(line.Text);
            IReadOnlyList<string> rejects = value.All(ch => !char.IsLetter(ch))
                ? new[] { "numeric-only" }
                : Array.Empty<string>();
            candidates.Add(Create("ReceiverName", value, line, layout, "recipient-zone", 25, ["recipient-label"], rejects));
        }
    }

    private static void AddSigner(OcrLine line, DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        var section = GetSection(layout, line.Index);
        if (line.NormalizedText.Contains("NGUOI KY"))
        {
            candidates.Add(Create("SignerName", ValueAfterSeparator(line.Text), line, layout, "signer-label", 35, ["label:nguoi-ky"], []));
            return;
        }

        var titleLine = layout.Lines.LastOrDefault(candidate =>
            candidate.Index < line.Index &&
            VietnameseOcrLexicon.SignatureTitles.Any(candidate.NormalizedText.Contains));
        if (titleLine != null)
        {
            IReadOnlyList<string> rejects = section == SectionType.Body
                ? new[] { "body-section" }
                : Array.Empty<string>();
            candidates.Add(Create("SignerName", line.Text, line, layout, "signature-neighborhood", 20, ["near:title"], rejects));
        }
    }

    private static void AddTitle(DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        foreach (var line in layout.Lines.Where(line => line.NormalizedText.StartsWith("TRICH YEU") || line.NormalizedText.StartsWith("VE VIEC")))
        {
            candidates.Add(new(
                "Title",
                ValueAfterSeparator(line.Text),
                line.Text,
                "explicit-title-label",
                45,
                line.Index,
                GetSection(layout, line.Index),
                ["label:title"],
                []));
        }

        foreach (var block in layout.Blocks.Where(block => block.SectionType == SectionType.Title))
            candidates.Add(new("Title", block.Text, block.Text, "title-block", 25, block.Lines[0].Index, block.SectionType, ["role:title"], []));
    }

    private static void AddBody(DocumentLayout layout, ICollection<ExtractedFieldCandidate> candidates)
    {
        var body = layout.Blocks
            .Where(block => block.SectionType is SectionType.Body or SectionType.LegalBasis or SectionType.Articles)
            .Select(block => block.Text)
            .ToList();
        if (body.Count == 0)
            return;
        var text = string.Join(Environment.NewLine, body);
        var first = layout.Blocks.First(block => block.SectionType is SectionType.Body or SectionType.LegalBasis or SectionType.Articles);
        candidates.Add(new("BodyText", text, text, "body-blocks", 35, first.Lines[0].Index, SectionType.Body, ["section:body"], []));
    }

    private static ExtractedFieldCandidate Create(
        string fieldName,
        string value,
        OcrLine line,
        DocumentLayout layout,
        string method,
        double baseScore,
        IReadOnlyList<string> signals,
        IReadOnlyList<string> rejects)
        => new(fieldName, value, line.Text, method, baseScore, line.Index, GetSection(layout, line.Index), signals, rejects);

    private static SectionType GetSection(DocumentLayout layout, int lineIndex)
        => layout.Blocks.FirstOrDefault(block => block.Lines.Any(line => line.Index == lineIndex))?.SectionType ?? SectionType.Unknown;

    private static string ValueAfterSeparator(string value)
    {
        var index = value.IndexOfAny([':', '-']);
        return index >= 0 ? value[(index + 1)..].Trim() : value.Trim();
    }
}

public sealed class VietnameseConfidenceScorer : IConfidenceScorer
{
    public double Score(ExtractedFieldCandidate candidate, DocumentLayout layout)
    {
        var score = candidate.BaseScore;
        if (candidate.FieldName == "IssueDate")
        {
            if (candidate.Signals.Contains("contains:ky-ngay")) score += 40;
            if (candidate.Signals.Contains("region:header")) score += 25;
            if (candidate.Signals.Contains("format:official-date")) score += 30;
            if (candidate.RejectionReasons.Any(reason => VietnameseOcrLexicon.IssueDateRejectors.Any(reason.Contains))) score -= 80;
        }
        if (candidate.FieldName == "SignerName" && candidate.Signals.Contains("near:title")) score += 30;
        if (candidate.RejectionReasons.Contains("body-section")) score -= 80;
        if (candidate.RejectionReasons.Contains("numeric-only")) score -= 70;
        return Math.Clamp(score / 100d, 0, 1);
    }
}

public sealed class VietnameseSemanticParser : ISemanticParser
{
    private readonly IConfidenceScorer _scorer;

    public VietnameseSemanticParser(IConfidenceScorer? scorer = null)
    {
        _scorer = scorer ?? new VietnameseConfidenceScorer();
    }

    public SemanticParseResult Parse(DocumentLayout layout, DocumentClassification classification, IReadOnlyList<ExtractedFieldCandidate> candidates)
    {
        var trace = new List<ExtractionTraceEntry>
        {
            new("classifier", $"kind={classification.Kind}; confidence={classification.Confidence:0.00}")
        };

        var fields = new Dictionary<string, ExtractedFieldValue>();
        foreach (var group in candidates.GroupBy(candidate => candidate.FieldName))
        {
            var scored = group.Select(candidate => (Candidate: candidate, Score: _scorer.Score(candidate, layout))).ToList();
            foreach (var rejected in scored.Where(item => item.Candidate.RejectionReasons.Count > 0))
                trace.Add(new(group.Key, $"rejected: {rejected.Candidate.Value}; reason={string.Join(",", rejected.Candidate.RejectionReasons)}"));
            var winner = scored.OrderByDescending(item => item.Score).ThenBy(item => item.Candidate.LineIndex).First();
            trace.Add(new(group.Key, $"accepted: {winner.Candidate.Value}; score={winner.Score:0.00}; reason={string.Join(",", winner.Candidate.Signals)}"));
            fields[group.Key] = new ExtractedFieldValue(
                group.Key,
                winner.Candidate.Value,
                winner.Score,
                winner.Candidate.SourceText,
                winner.Candidate.ExtractionMethod,
                winner.Score < 0.80);
        }

        return new SemanticParseResult(fields, trace);
    }
}

public sealed class VietnameseValidationService : IValidationService
{
    public ValidationResult Validate(DocumentLayout layout, SemanticParseResult semanticResult)
    {
        var fields = semanticResult.Fields.ToDictionary(pair => pair.Key, pair => pair.Value);
        var reasons = new List<string>();
        foreach (var field in fields.Values.Where(field => field.RequiresReview))
            reasons.Add($"{field.FieldName} có độ tin cậy thấp ({field.Confidence:0.00}).");
        return new ValidationResult(fields, reasons.Count > 0, reasons);
    }
}
