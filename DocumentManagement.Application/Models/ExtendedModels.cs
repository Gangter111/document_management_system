namespace DocumentManagement.Application.Models;

public class AutoFillDocumentResult
{
    public string? DocumentNumber { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? IssueDate { get; set; }
    public string? SenderName { get; set; }
    public string? ReceiverName { get; set; }
    public string? UrgencyLevel { get; set; }
    public string? ContentText { get; set; }
    public bool IsFromOcr { get; set; }
}
