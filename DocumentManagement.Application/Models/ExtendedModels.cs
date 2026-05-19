namespace DocumentManagement.Application.Models;

public class AutoFillDocumentResult
{
    public string? DocumentNumber { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? IssueDate { get; set; }
    public string? SenderName { get; set; }
    public string? ReceiverName { get; set; }
    public string? SignerName { get; set; }
    public string? UrgencyLevel { get; set; }
    public string? ContentText { get; set; }
    public string? RawText { get; set; }
    public bool IsFromOcr { get; set; }
    public double? OcrConfidence { get; set; }
    public bool IsPartialExtraction { get; set; }
    public string? FailureKind { get; set; }
    public string? FailureMessage { get; set; }
    public string? DocumentKind { get; set; }
    public bool RequiresManualReview { get; set; }
    public IReadOnlyList<ExtractedFieldValue> Fields { get; set; } = Array.Empty<ExtractedFieldValue>();
    public IReadOnlyList<string> ReviewReasons { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ExtractionTraceEntry> ExtractionTrace { get; set; } = Array.Empty<ExtractionTraceEntry>();
}

public static class PdfExtractionFailureKind
{
    public const string Unsupported = "unsupported";
    public const string Encrypted = "encrypted";
    public const string ScannedImageOnly = "scanned_image_only";
    public const string Malformed = "malformed";
    public const string TooLarge = "too_large";
    public const string Timeout = "timeout";
}

public sealed class PdfExtractionException : Exception
{
    public PdfExtractionException(string failureKind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public string FailureKind { get; }
}

public sealed record PdfExtractionJobRequest(
    string CorrelationId,
    string FileToken,
    string FilePath,
    long FileSize,
    TimeSpan Timeout);

public sealed record PdfRenderedPageImage(
    int PageNumber,
    string ImagePath,
    long FileSize);

public sealed record PdfOcrPageResult(
    string Text,
    double? Confidence,
    IReadOnlyList<OcrLine>? Lines = null);

public sealed record BoundingBox(
    int X,
    int Y,
    int Width,
    int Height);

public sealed record OcrLine(
    int Index,
    int PageIndex,
    string Text,
    string NormalizedText,
    BoundingBox BoundingBox,
    double Confidence);

public enum SectionType
{
    Unknown,
    NationalHeader,
    Header,
    Title,
    LegalBasis,
    Body,
    Articles,
    Recipient,
    Signature,
    Footer
}

public enum BlockRole
{
    Unknown,
    Organization,
    NationalMotto,
    Metadata,
    Title,
    Paragraph,
    RecipientList,
    SignatureTitle,
    SignerName
}

public sealed record TextBlock(
    int Index,
    int PageIndex,
    string Text,
    string NormalizedText,
    BoundingBox BoundingBox,
    double Confidence,
    SectionType SectionType,
    BlockRole Role,
    IReadOnlyList<OcrLine> Lines);

public sealed record DocumentLayout(
    string RawText,
    string NormalizedText,
    IReadOnlyList<OcrLine> Lines,
    IReadOnlyList<TextBlock> Blocks);

public sealed record OcrDocument(
    string RawText,
    IReadOnlyList<OcrLine> Lines);

public sealed record DocumentClassification(
    string Kind,
    double Confidence,
    IReadOnlyDictionary<string, double> Scores);

public sealed record ExtractedFieldCandidate(
    string FieldName,
    string Value,
    string SourceText,
    string ExtractionMethod,
    double BaseScore,
    int LineIndex,
    SectionType SectionType,
    IReadOnlyList<string> Signals,
    IReadOnlyList<string> RejectionReasons);

public sealed record ExtractedFieldValue(
    string FieldName,
    string? Value,
    double Confidence,
    string? SourceText,
    string ExtractionMethod,
    bool RequiresReview);

public sealed record SemanticParseResult(
    IReadOnlyDictionary<string, ExtractedFieldValue> Fields,
    IReadOnlyList<ExtractionTraceEntry> Trace);

public sealed record ValidationResult(
    IReadOnlyDictionary<string, ExtractedFieldValue> Fields,
    bool RequiresManualReview,
    IReadOnlyList<string> ReviewReasons);

public sealed record ExtractionTraceEntry(
    string Stage,
    string Message);
