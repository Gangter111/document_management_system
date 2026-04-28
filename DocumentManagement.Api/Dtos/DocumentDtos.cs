namespace DocumentRegistry.Api.Dtos;

public class DocumentCreateRequest
{
    public string DocumentType { get; set; } = string.Empty;

    public string DocumentNumber { get; set; } = string.Empty;

    public string? ReferenceNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public DateTime? IssuedDate { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public DateTime? ExpiredDate { get; set; }

    public string? IssuingOrganization { get; set; }

    public string? Signer { get; set; }

    public string? ResponsibleDepartment { get; set; }

    public string? ConfidentialLevel { get; set; }

    public string? UrgencyLevel { get; set; }

    public string Status { get; set; } = "Active";

    public string? Note { get; set; }
}

public class DocumentUpdateRequest : DocumentCreateRequest
{
}

public class DocumentSearchRequest
{
    public string? Keyword { get; set; }

    public string? DocumentNumber { get; set; }

    public string? Signer { get; set; }

    public string? IssuingOrganization { get; set; }

    public string? DocumentType { get; set; }

    public string? UrgencyLevel { get; set; }

    public string? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}