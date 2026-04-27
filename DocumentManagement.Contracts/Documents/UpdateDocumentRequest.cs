namespace DocumentManagement.Contracts.Documents;

public class UpdateDocumentRequest
{
    public long Id { get; set; }

    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ContentText { get; set; }

    public string? IssueDate { get; set; }
    public string? ReceivedDate { get; set; }
    public string? DueDate { get; set; }

    public string? SenderName { get; set; }
    public string? ReceiverName { get; set; }
    public string? SignerName { get; set; }

    public long? CategoryId { get; set; }
    public long? StatusId { get; set; } = 4;

    public string ConfidentialityLevel { get; set; } = "NORMAL";
    public string UrgencyLevel { get; set; } = "NORMAL";

    public string? ProcessingDepartment { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }

    public bool IsExpired { get; set; }
    public string OcrStatus { get; set; } = "PENDING";
    public string? UpdatedBy { get; set; }
}
