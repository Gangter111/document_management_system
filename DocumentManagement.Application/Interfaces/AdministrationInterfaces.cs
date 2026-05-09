using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface ICatalogRepository
{
    Task<IReadOnlyList<CatalogItemModel>> GetCategoriesAsync(bool includeInactive);

    Task<IReadOnlyList<CatalogItemModel>> GetStatusesAsync(bool includeInactive);

    Task<CatalogItemModel> CreateCategoryAsync(SaveCatalogItemModel request);

    Task<CatalogItemModel> UpdateCategoryAsync(long id, SaveCatalogItemModel request);

    Task DeleteCategoryAsync(long id);

    Task<CatalogItemModel> CreateStatusAsync(SaveCatalogItemModel request);

    Task<CatalogItemModel> UpdateStatusAsync(long id, SaveCatalogItemModel request);

    Task DeleteStatusAsync(long id);
}

public interface IUserManagementRepository
{
    Task<IReadOnlyList<UserAdminModel>> SearchUsersAsync(string? keyword, bool includeInactive);

    Task<IReadOnlyList<RoleModel>> GetRolesAsync();

    Task<UserAdminModel> CreateUserAsync(SaveUserModel request);

    Task<UserAdminModel> UpdateUserAsync(long id, SaveUserModel request);

    Task DeleteUserAsync(long id);
}
