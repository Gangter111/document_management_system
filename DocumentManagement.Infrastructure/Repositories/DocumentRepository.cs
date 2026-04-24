using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DocumentManagement.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DocumentRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> CreateAsync(Document document)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO documents
(
    document_type,
    document_number,
    title,
    issue_date,
    category_id,
    status_id,
    urgency_level,
    confidentiality_level,
    processing_department,
    assigned_to,
    is_active,
    created_at,
    updated_at
)
VALUES
(
    $document_type,
    $document_number,
    $title,
    $issue_date,
    $category_id,
    $status_id,
    $urgency_level,
    $confidentiality_level,
    $processing_department,
    $assigned_to,
    $is_active,
    $created_at,
    $updated_at
);

SELECT last_insert_rowid();";

        BindDocument(command, document, includeCreatedAt: true);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result ?? 0L);
    }

    public async Task<bool> UpdateAsync(Document document)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE documents SET
    document_type = $document_type,
    document_number = $document_number,
    title = $title,
    issue_date = $issue_date,
    category_id = $category_id,
    status_id = $status_id,
    urgency_level = $urgency_level,
    confidentiality_level = $confidentiality_level,
    processing_department = $processing_department,
    assigned_to = $assigned_to,
    is_active = $is_active,
    updated_at = $updated_at
WHERE id = $id;";

        BindDocument(command, document, includeCreatedAt: false);
        command.Parameters.AddWithValue("$id", GetPropertyValue<long>(document, "Id"));

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM documents WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<Document?> GetByIdAsync(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM documents WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return Map(reader);

        return null;
    }

    public async Task<List<Document>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM documents ORDER BY created_at DESC;";

        using var reader = await command.ExecuteReaderAsync();
        var result = new List<Document>();

        while (await reader.ReadAsync())
            result.Add(Map(reader));

        return result;
    }

    public async Task<List<Document>> SearchAsync(DocumentSearchRequest request)
    {
        var paged = await SearchPagedAsync(request);
        return paged.Items.ToList();
    }

    public async Task<PagedResult<Document>> SearchPagedAsync(DocumentSearchRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var where = new StringBuilder("WHERE 1 = 1");

        if (!string.IsNullOrWhiteSpace(request.Keyword))
            where.Append(" AND (title LIKE $keyword OR document_number LIKE $keyword)");

        if (request.CategoryId.HasValue)
            where.Append(" AND category_id = $categoryId");

        if (!string.IsNullOrWhiteSpace(request.UrgencyLevel))
            where.Append(" AND urgency_level = $urgencyLevel");

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM documents {where};";
        BindSearch(countCommand, request);

        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync() ?? 0);

        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT *
FROM documents
{where}
ORDER BY DATETIME(COALESCE(updated_at, created_at)) DESC
LIMIT $pageSize OFFSET $offset;";

        BindSearch(command, request);
        command.Parameters.AddWithValue("$pageSize", request.PageSize);
        command.Parameters.AddWithValue("$offset", (request.PageNumber - 1) * request.PageSize);

        using var reader = await command.ExecuteReaderAsync();
        var items = new List<Document>();

        while (await reader.ReadAsync())
            items.Add(Map(reader));

        return new PagedResult<Document>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM document_categories WHERE is_active = 1 ORDER BY name;";

        using var reader = await command.ExecuteReaderAsync();
        var items = new List<CategoryModel>();

        while (await reader.ReadAsync())
        {
            items.Add(new CategoryModel
            {
                Id = Convert.ToInt64(reader["id"]),
                Name = reader["name"]?.ToString() ?? string.Empty
            });
        }

        return items;
    }

    public async Task<List<StatusModel>> GetStatusesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM document_statuses WHERE is_active = 1 ORDER BY id;";

        using var reader = await command.ExecuteReaderAsync();
        var items = new List<StatusModel>();

        while (await reader.ReadAsync())
        {
            items.Add(new StatusModel
            {
                Id = Convert.ToInt64(reader["id"]),
                Name = reader["name"]?.ToString() ?? string.Empty
            });
        }

        return items;
    }

    private static void BindSearch(SqliteCommand command, DocumentSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            command.Parameters.AddWithValue("$keyword", $"%{request.Keyword}%");

        if (request.CategoryId.HasValue)
            command.Parameters.AddWithValue("$categoryId", request.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(request.UrgencyLevel))
            command.Parameters.AddWithValue("$urgencyLevel", request.UrgencyLevel);
    }

    private static void BindDocument(SqliteCommand command, Document document, bool includeCreatedAt)
    {
        command.Parameters.AddWithValue("$document_type", ToDbValue(GetPropertyValue<string?>(document, "DocumentType")));
        command.Parameters.AddWithValue("$document_number", ToDbValue(GetPropertyValue<string?>(document, "DocumentNumber")));
        command.Parameters.AddWithValue("$title", ToDbValue(GetPropertyValue<string?>(document, "Title")));
        command.Parameters.AddWithValue("$issue_date", ToDbValue(GetPropertyValueAsString(document, "IssueDate")));
        command.Parameters.AddWithValue("$category_id", ToDbValue(GetPropertyValue<object?>(document, "CategoryId")));
        command.Parameters.AddWithValue("$status_id", ToDbValue(GetPropertyValue<object?>(document, "StatusId")));
        command.Parameters.AddWithValue("$urgency_level", ToDbValue(GetPropertyValue<string?>(document, "UrgencyLevel")));
        command.Parameters.AddWithValue("$confidentiality_level", ToDbValue(GetPropertyValue<string?>(document, "ConfidentialityLevel")));
        command.Parameters.AddWithValue("$processing_department", ToDbValue(GetPropertyValue<string?>(document, "ProcessingDepartment")));
        command.Parameters.AddWithValue("$assigned_to", ToDbValue(GetPropertyValue<string?>(document, "AssignedTo")));
        command.Parameters.AddWithValue("$is_active", GetBoolValue(document, "IsActive") ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        if (includeCreatedAt)
        {
            var createdAt = GetPropertyValueAsString(document, "CreatedAt");
            if (string.IsNullOrWhiteSpace(createdAt))
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            command.Parameters.AddWithValue("$created_at", createdAt);
        }
    }

    private static Document Map(SqliteDataReader reader)
    {
        var document = new Document();

        SetPropertyIfExists(document, "Id", Convert.ToInt64(reader["id"]));
        SetPropertyIfExists(document, "DocumentType", reader["document_type"]?.ToString());
        SetPropertyIfExists(document, "DocumentNumber", reader["document_number"]?.ToString());
        SetPropertyIfExists(document, "Title", reader["title"]?.ToString());
        SetPropertyIfExists(document, "IssueDate", reader["issue_date"] == DBNull.Value ? null : reader["issue_date"]?.ToString());
        SetPropertyIfExists(document, "CategoryId", reader["category_id"] == DBNull.Value ? null : reader["category_id"]);
        SetPropertyIfExists(document, "StatusId", reader["status_id"] == DBNull.Value ? null : reader["status_id"]);
        SetPropertyIfExists(document, "UrgencyLevel", reader["urgency_level"]?.ToString());
        SetPropertyIfExists(document, "ConfidentialityLevel", reader["confidentiality_level"]?.ToString());
        SetPropertyIfExists(document, "ProcessingDepartment", reader["processing_department"]?.ToString());
        SetPropertyIfExists(document, "AssignedTo", reader["assigned_to"]?.ToString());
        SetPropertyIfExists(document, "IsActive", reader["is_active"] != DBNull.Value && Convert.ToInt32(reader["is_active"]) == 1);
        SetPropertyIfExists(document, "CreatedAt", reader["created_at"] == DBNull.Value ? null : reader["created_at"]?.ToString());
        SetPropertyIfExists(document, "UpdatedAt", reader["updated_at"] == DBNull.Value ? null : reader["updated_at"]?.ToString());

        return document;
    }

    private static object ToDbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static T GetPropertyValue<T>(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            return default!;

        var value = prop.GetValue(obj);
        if (value == null)
            return default!;

        if (value is T tValue)
            return tValue;

        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static string? GetPropertyValueAsString(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            return null;

        var value = prop.GetValue(obj);
        return value?.ToString();
    }

    private static bool GetBoolValue(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            return true;

        var value = prop.GetValue(obj);
        if (value == null)
            return true;

        if (value is bool b)
            return b;

        return Convert.ToBoolean(value);
    }

    private static void SetPropertyIfExists(object obj, string propertyName, object? value)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || !prop.CanWrite)
            return;

        if (value == null || value == DBNull.Value)
        {
            if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                prop.SetValue(obj, null);
            return;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            object convertedValue;

            if (targetType == typeof(string))
            {
                convertedValue = value.ToString() ?? string.Empty;
            }
            else if (targetType == typeof(long))
            {
                convertedValue = Convert.ToInt64(value);
            }
            else if (targetType == typeof(int))
            {
                convertedValue = Convert.ToInt32(value);
            }
            else if (targetType == typeof(bool))
            {
                convertedValue = value is bool b ? b : Convert.ToInt32(value) == 1;
            }
            else if (targetType == typeof(DateTime))
            {
                convertedValue = value is DateTime dt ? dt : DateTime.Parse(value.ToString()!);
            }
            else
            {
                convertedValue = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            prop.SetValue(obj, convertedValue);
        }
        catch
        {
            // Bỏ qua field không map được để tránh compile/runtime gãy toàn repo.
        }
    }
}