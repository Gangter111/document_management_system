namespace DocumentRegistry.Api.Models;

public class Document
{
    public int Id { get; set; }

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

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? DeletedByUserId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
}