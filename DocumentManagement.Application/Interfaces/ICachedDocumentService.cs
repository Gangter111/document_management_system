using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface ICachedDocumentService
{
    Task<IReadOnlyList<Document>> GetCachedDocumentsAsync();
    Task<Document?> GetCachedDocumentByIdAsync(long id);
    void InvalidateCache();
}