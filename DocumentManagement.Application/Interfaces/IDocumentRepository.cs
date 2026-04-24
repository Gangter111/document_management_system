using DocumentManagement.Domain.Entities;
using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IDocumentRepository
{
    Task<long> CreateAsync(Document document);
    Task<bool> UpdateAsync(Document document);
    Task<bool> DeleteAsync(long id);
    Task<Document?> GetByIdAsync(long id);
    Task<List<Document>> GetAllAsync();

    // Tìm kiếm kiểu cũ - giữ lại để tương thích code cũ
    Task<List<Document>> SearchAsync(DocumentSearchRequest request);

    // Tìm kiếm phân trang - dùng cho màn hình danh sách lớn
    Task<PagedResult<Document>> SearchPagedAsync(DocumentSearchRequest request);

    Task<List<CategoryModel>> GetCategoriesAsync();
    Task<List<StatusModel>> GetStatusesAsync();
}