namespace DocumentManagement.Application.Models;

public class DocumentSearchRequest
{
    public string? Keyword { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentType { get; set; }
    public string? SenderName { get; set; }
    public string? ReceiverName { get; set; }

    public string? FromIssueDate { get; set; }
    public string? ToIssueDate { get; set; }

    public long? CategoryId { get; set; }
    public long? StatusId { get; set; }

    public string? ConfidentialityLevel { get; set; }
    public string? UrgencyLevel { get; set; }
    public string? ProcessingDepartment { get; set; }
    public string? AssignedTo { get; set; }

    public string? FromDate { get; set; }
    public string? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}