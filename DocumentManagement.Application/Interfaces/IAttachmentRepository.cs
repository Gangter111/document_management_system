using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface IAttachmentRepository
{
    Task<long> CreateAsync(DocumentAttachment attachment);
    Task<List<DocumentAttachment>> GetByDocumentIdAsync(long documentId);
    Task<bool> DeleteByIdAsync(long id);
}
