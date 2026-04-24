using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface IAttachmentService
{
    Task<long> UploadAsync(long documentId, string sourceFilePath);
    Task<List<DocumentAttachment>> GetByDocumentIdAsync(long documentId);
}
