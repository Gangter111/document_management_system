namespace DocumentManagement.Domain.Entities;

public class DocumentAttachment
{
    public long Id { get; set; }
    public long DocumentId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public string? MimeType { get; set; }
    public long FileSize { get; set; }
    public string? FileHash { get; set; }

    public string? ExtractedText { get; set; }
    public string UploadDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
