namespace DocumentManagement.Contracts.Dashboard;

public class RecentDocumentDto
{
    public long Id { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string UrgencyText { get; set; } = string.Empty;

    public string? DueDate { get; set; }

    public string DocumentDate { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
