using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentManagement.Application.Models;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface IDocumentService
{
    Task<IEnumerable<Document>> GetAllAsync();

    Task<IEnumerable<Document>> SearchAsync(DocumentSearchRequest request);

    Task<PagedResult<Document>> SearchPagedAsync(DocumentSearchRequest request);

    Task<Document?> GetByIdAsync(long id);

    Task<long> CreateAsync(Document document);

    Task UpdateAsync(Document document);

    Task SoftDeleteAsync(long id);

    // Workflow
    Task SubmitForApprovalAsync(long id);

    Task ApproveAsync(long id);

    Task RejectAsync(long id, string? reason);

    Task<List<CategoryModel>> GetCategoriesAsync();

    Task<List<StatusModel>> GetStatusesAsync();
}