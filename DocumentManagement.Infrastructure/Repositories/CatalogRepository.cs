using System.Data.Common;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private static readonly HashSet<long> ProtectedStatusIds = new() { 1, 2, 3, 4, 5, 6 };

    private readonly IDbConnectionFactory _connectionFactory;

    public CatalogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<CatalogItemModel>> GetCategoriesAsync(bool includeInactive)
    {
        return GetItemsAsync("document_categories", includeInactive, supportsCode: false);
    }

    public Task<IReadOnlyList<CatalogItemModel>> GetStatusesAsync(bool includeInactive)
    {
        return GetItemsAsync("document_statuses", includeInactive, supportsCode: true);
    }

    public Task<CatalogItemModel> CreateCategoryAsync(SaveCatalogItemModel request)
    {
        return CreateItemAsync("document_categories", request, supportsCode: false);
    }

    public Task<CatalogItemModel> UpdateCategoryAsync(long id, SaveCatalogItemModel request)
    {
        return UpdateItemAsync("document_categories", id, request, supportsCode: false);
    }

    public Task DeleteCategoryAsync(long id)
    {
        return SoftDeleteItemAsync("document_categories", id);
    }

    public Task<CatalogItemModel> CreateStatusAsync(SaveCatalogItemModel request)
    {
        return CreateItemAsync("document_statuses", request, supportsCode: true);
    }

    public Task<CatalogItemModel> UpdateStatusAsync(long id, SaveCatalogItemModel request)
    {
        return UpdateItemAsync("document_statuses", id, request, supportsCode: true);
    }

    public Task DeleteStatusAsync(long id)
    {
        return SoftDeleteItemAsync("document_statuses", id);
    }

    private async Task<IReadOnlyList<CatalogItemModel>> GetItemsAsync(
        string tableName,
        bool includeInactive,
        bool supportsCode)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT id, {(supportsCode ? "code" : "''")} AS code, name, is_active
FROM {tableName}
WHERE (@includeInactive = 1 OR is_active = 1)
ORDER BY id;";
        command.AddParameter("includeInactive", includeInactive ? 1 : 0);

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<CatalogItemModel>();

        while (await reader.ReadAsync())
        {
            items.Add(MapCatalogItem(reader));
        }

        return items;
    }

    private async Task<CatalogItemModel> CreateItemAsync(
        string tableName,
        SaveCatalogItemModel request,
        bool supportsCode)
    {
        ValidateRequest(request, supportsCode);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureNameIsUniqueAsync(connection, tableName, request.Name, null);
        await EnsureCodeIsUniqueAsync(connection, tableName, request.Code, null, supportsCode);

        await using var command = connection.CreateCommand();

        if (supportsCode)
        {
            command.CommandText = $@"
INSERT INTO {tableName} (code, name, is_active)
VALUES (@code, @name, @isActive);
{IdentitySelectSql}";
            command.AddParameter("code", request.Code.Trim());
        }
        else
        {
            command.CommandText = $@"
INSERT INTO {tableName} (name, is_active)
VALUES (@name, @isActive);
{IdentitySelectSql}";
        }

        command.AddParameter("name", request.Name.Trim());
        command.AddParameter("isActive", request.IsActive ? 1 : 0);

        var id = Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0);
        return await GetRequiredItemAsync(connection, tableName, id, supportsCode);
    }

    private async Task<CatalogItemModel> UpdateItemAsync(
        string tableName,
        long id,
        SaveCatalogItemModel request,
        bool supportsCode)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException("Id khong hop le.");
        }

        ValidateRequest(request, supportsCode);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureProtectedStatusIsNotCorruptedAsync(connection, tableName, id, request, supportsCode);
        await EnsureNameIsUniqueAsync(connection, tableName, request.Name, id);
        await EnsureCodeIsUniqueAsync(connection, tableName, request.Code, id, supportsCode);

        await using var command = connection.CreateCommand();

        if (supportsCode)
        {
            command.CommandText = $@"
UPDATE {tableName}
SET code = @code,
    name = @name,
    is_active = @isActive
WHERE id = @id;";
            command.AddParameter("code", request.Code.Trim());
        }
        else
        {
            command.CommandText = $@"
UPDATE {tableName}
SET name = @name,
    is_active = @isActive
WHERE id = @id;";
        }

        command.AddParameter("name", request.Name.Trim());
        command.AddParameter("isActive", request.IsActive ? 1 : 0);
        command.AddParameter("id", id);

        var affected = await command.ExecuteNonQueryAsync();

        if (affected == 0)
        {
            throw new InvalidOperationException("Khong tim thay danh muc.");
        }

        return await GetRequiredItemAsync(connection, tableName, id, supportsCode);
    }

    private async Task SoftDeleteItemAsync(string tableName, long id)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException("Id khong hop le.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureCanSoftDeleteAsync(connection, tableName, id);

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
UPDATE {tableName}
SET is_active = 0
WHERE id = @id;";
        command.AddParameter("id", id);

        var affected = await command.ExecuteNonQueryAsync();

        if (affected == 0)
        {
            throw new InvalidOperationException("Khong tim thay danh muc.");
        }
    }

    private async Task<CatalogItemModel> GetRequiredItemAsync(
        DbConnection connection,
        string tableName,
        long id,
        bool supportsCode)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT id, {(supportsCode ? "code" : "''")} AS code, name, is_active
FROM {tableName}
WHERE id = @id;";
        command.AddParameter("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapCatalogItem(reader);
        }

        throw new InvalidOperationException("Khong tim thay danh muc.");
    }

    private async Task EnsureNameIsUniqueAsync(
        DbConnection connection,
        string tableName,
        string name,
        long? excludedId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(*)
FROM {tableName}
WHERE LOWER(TRIM(name)) = LOWER(TRIM(@name))
  AND (@excludedId IS NULL OR id <> @excludedId);";
        command.AddParameter("name", name.Trim());
        command.AddParameter("excludedId", excludedId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        if (count > 0)
        {
            throw new InvalidOperationException("Ten danh muc da ton tai.");
        }
    }

    private async Task EnsureCodeIsUniqueAsync(
        DbConnection connection,
        string tableName,
        string code,
        long? excludedId,
        bool supportsCode)
    {
        if (!supportsCode)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(*)
FROM {tableName}
WHERE LOWER(TRIM(code)) = LOWER(TRIM(@code))
  AND (@excludedId IS NULL OR id <> @excludedId);";
        command.AddParameter("code", code.Trim());
        command.AddParameter("excludedId", excludedId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        if (count > 0)
        {
            throw new InvalidOperationException("Ma trang thai da ton tai.");
        }
    }

    private static async Task EnsureProtectedStatusIsNotCorruptedAsync(
        DbConnection connection,
        string tableName,
        long id,
        SaveCatalogItemModel request,
        bool supportsCode)
    {
        if (!supportsCode || !string.Equals(tableName, "document_statuses", StringComparison.OrdinalIgnoreCase)
            || !ProtectedStatusIds.Contains(id))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code FROM document_statuses WHERE id = @id;";
        command.AddParameter("id", id);

        var currentCode = (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;

        if (!string.Equals(currentCode, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Khong duoc thay doi ma trang thai he thong.");
        }
    }

    private static async Task EnsureCanSoftDeleteAsync(DbConnection connection, string tableName, long id)
    {
        if (string.Equals(tableName, "document_statuses", StringComparison.OrdinalIgnoreCase)
            && ProtectedStatusIds.Contains(id))
        {
            throw new InvalidOperationException("Khong duoc ngung su dung trang thai he thong.");
        }

        if (!string.Equals(tableName, "document_categories", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM documents
WHERE is_active = 1
  AND category_id = @id;";
        command.AddParameter("id", id);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        if (count > 0)
        {
            throw new InvalidOperationException("Khong duoc ngung su dung danh muc dang duoc gan cho van ban.");
        }
    }

    private string IdentitySelectSql => _connectionFactory.Provider == DatabaseProvider.SqlServer
        ? "SELECT CAST(SCOPE_IDENTITY() AS bigint);"
        : "SELECT last_insert_rowid();";

    private static void ValidateRequest(SaveCatalogItemModel request, bool supportsCode)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Ten danh muc khong duoc de trong.");
        }

        if (supportsCode && string.IsNullOrWhiteSpace(request.Code))
        {
            throw new InvalidOperationException("Ma trang thai khong duoc de trong.");
        }
    }

    private static CatalogItemModel MapCatalogItem(DbDataReader reader)
    {
        return new CatalogItemModel
        {
            Id = Convert.ToInt64(reader["id"]),
            Code = reader["code"]?.ToString() ?? string.Empty,
            Name = reader["name"]?.ToString() ?? string.Empty,
            IsActive = Convert.ToInt32(reader["is_active"]) == 1
        };
    }
}
