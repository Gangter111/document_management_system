namespace DocumentManagement.Contracts.Documents;

public class DocumentDto
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
    public long? StatusId { get; set; }

    public string ConfidentialityLevel { get; set; } = "NORMAL";
    public string UrgencyLevel { get; set; } = "NORMAL";

    public string? ProcessingDepartment { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsExpired { get; set; }

    public string OcrStatus { get; set; } = "PENDING";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public string StatusCode { get; set; } = "ISSUED";
    public string StatusText { get; set; } = "Đã ban hành";
    public string StatusColor { get; set; } = "#10B981";

    public string UrgencyText { get; set; } = "THƯỜNG";
    public string UrgencyColor { get; set; } = "#10B981";

    public string DocumentDate { get; set; } = string.Empty;
}
