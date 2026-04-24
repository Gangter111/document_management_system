namespace DocumentManagement.Domain.Enums;

public enum DocumentStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Issued = 4,
    Archived = 5,
    Rejected = 6
}