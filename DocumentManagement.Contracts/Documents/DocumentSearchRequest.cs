namespace DocumentManagement.Contracts.Documents;

public class DocumentSearchRequest
{
    public string? Keyword { get; set; }

    public long? CategoryId { get; set; }

    public long? StatusId { get; set; }

    public string? UrgencyLevel { get; set; }

    public string? FromDate { get; set; }

    public string? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 100;
}
