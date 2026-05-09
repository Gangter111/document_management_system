using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IHistoryRepository
{
    Task AddAsync(
        long? documentId,
        string actionType,
        string? actionDescription,
        string? oldValue,
        string? newValue,
        string? actionBy);

    Task<List<DocumentHistoryModel>> GetByDocumentIdAsync(long documentId);
}
