namespace DocumentManagement.Application.Models;

public class RecentDocumentItem
{
    public long Id { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
}