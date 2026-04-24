namespace DocumentManagement.Application.Models;

public class UpdateDocumentRequest : CreateDocumentRequest
{
    public long Id { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsExpired { get; set; } = false;
    public string OcrStatus { get; set; } = "PENDING";
    public string? UpdatedBy { get; set; }
}
