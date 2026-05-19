namespace DocumentManagement.Contracts.Documents;

public class AutoFillDocumentResultDto
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

    public bool IsFromOcr { get; set; }

    public double? OcrConfidence { get; set; }

    public bool IsPartialExtraction { get; set; }

    public string? FailureKind { get; set; }

    public string? FailureMessage { get; set; }

    public string? DocumentKind { get; set; }

    public bool RequiresManualReview { get; set; }

    public List<ExtractedFieldDto> Fields { get; set; } = new();

    public List<string> ReviewReasons { get; set; } = new();

    public List<ExtractionTraceDto> ExtractionTrace { get; set; } = new();
}

public class ExtractedFieldDto
{
    public string FieldName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public double Confidence { get; set; }
    public string? SourceText { get; set; }
    public string ExtractionMethod { get; set; } = string.Empty;
    public bool RequiresReview { get; set; }
}

public class ExtractionTraceDto
{
    public string Stage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
