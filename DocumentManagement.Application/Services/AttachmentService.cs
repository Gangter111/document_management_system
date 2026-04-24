using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorageService;

    public AttachmentService(
        IAttachmentRepository attachmentRepository,
        IFileStorageService fileStorageService)
    {
        _attachmentRepository = attachmentRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<long> UploadAsync(long documentId, string sourceFilePath)
    {
        if (documentId <= 0)
            throw new ArgumentException("documentId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("sourceFilePath không được rỗng.");

        var saved = await _fileStorageService.SaveFileAsync(sourceFilePath);

        var attachment = new DocumentAttachment
        {
            DocumentId = documentId,
            OriginalFileName = Path.GetFileName(sourceFilePath),
            StoredFileName = saved.StoredFileName,
            StoredFilePath = saved.StoredFilePath,
            FileExtension = Path.GetExtension(sourceFilePath),
            MimeType = GetMimeType(sourceFilePath),
            FileSize = saved.FileSize,
            FileHash = saved.FileHash,
            UploadDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return await _attachmentRepository.CreateAsync(attachment);
    }

    public Task<List<DocumentAttachment>> GetByDocumentIdAsync(long documentId)
    {
        return _attachmentRepository.GetByDocumentIdAsync(documentId);
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
