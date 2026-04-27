using System.Globalization;
using System.Reflection;
using System.Text;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;
using Microsoft.Data.Sqlite;

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

        var columns = await GetDocumentColumnsAsync(connection);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var values = BuildDocumentColumnValues(document, now, includeId: false, includeCreatedAt: true);

        var insertColumns = values
            .Where(item => columns.Contains(item.Key))
            .ToList();

        using var command = connection.CreateCommand();

        command.CommandText = $@"
INSERT INTO documents
(
    {string.Join(",\n    ", insertColumns.Select(x => x.Key))}
)
VALUES
(
    {string.Join(",\n    ", insertColumns.Select(x => "$" + x.Key))}
);

SELECT last_insert_rowid();";

        foreach (var item in insertColumns)
        {
            command.Parameters.AddWithValue("$" + item.Key, ToDbValue(item.Value));
        }

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt64(result ?? 0L);
    }

    public async Task<bool> UpdateAsync(Document document)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var columns = await GetDocumentColumnsAsync(connection);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var values = BuildDocumentColumnValues(document, now, includeId: false, includeCreatedAt: false);

        var updateColumns = values
            .Where(item => columns.Contains(item.Key))
            .ToList();

        using var command = connection.CreateCommand();

        command.CommandText = $@"
UPDATE documents
SET
    {string.Join(",\n    ", updateColumns.Select(x => $"{x.Key} = ${x.Key}"))}
WHERE id = $id;";

        foreach (var item in updateColumns)
        {
            command.Parameters.AddWithValue("$" + item.Key, ToDbValue(item.Value));
        }

        command.Parameters.AddWithValue("$id", document.Id);

        var rows = await command.ExecuteNonQueryAsync();

        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var columns = await GetDocumentColumnsAsync(connection);

        using var command = connection.CreateCommand();

        if (columns.Contains("is_active"))
        {
            command.CommandText = @"
UPDATE documents
SET is_active = 0,
    updated_at = $updated_at
WHERE id = $id;";

            command.Parameters.AddWithValue("$updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        else
        {
            command.CommandText = "DELETE FROM documents WHERE id = $id;";
        }

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
        {
            return Map(reader);
        }

        return null;
    }

    public async Task<List<Document>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var columns = await GetDocumentColumnsAsync(connection);

        using var command = connection.CreateCommand();

        var where = columns.Contains("is_active")
            ? "WHERE is_active = 1"
            : string.Empty;

        var orderBy = columns.Contains("updated_at") && columns.Contains("created_at")
            ? "ORDER BY DATETIME(COALESCE(updated_at, created_at)) DESC"
            : "ORDER BY id DESC";

        command.CommandText = $@"
SELECT *
FROM documents
{where}
{orderBy};";

        using var reader = await command.ExecuteReaderAsync();

        var result = new List<Document>();

        while (await reader.ReadAsync())
        {
            result.Add(Map(reader));
        }

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

        var columns = await GetDocumentColumnsAsync(connection);

        var where = new StringBuilder("WHERE 1 = 1");

        if (columns.Contains("is_active"))
        {
            where.Append(" AND is_active = 1");
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keywordParts = new List<string>();

            if (columns.Contains("title"))
            {
                keywordParts.Add("title LIKE $keyword");
            }

            if (columns.Contains("document_number"))
            {
                keywordParts.Add("document_number LIKE $keyword");
            }

            if (columns.Contains("sender_name"))
            {
                keywordParts.Add("sender_name LIKE $keyword");
            }

            if (keywordParts.Count > 0)
            {
                where.Append(" AND (");
                where.Append(string.Join(" OR ", keywordParts));
                where.Append(")");
            }
        }

        if (request.CategoryId.HasValue && columns.Contains("category_id"))
        {
            where.Append(" AND category_id = $categoryId");
        }

        if (request.StatusId.HasValue && columns.Contains("status_id"))
        {
            where.Append(" AND status_id = $statusId");
        }

        if (!string.IsNullOrWhiteSpace(request.UrgencyLevel) && columns.Contains("urgency_level"))
        {
            where.Append(" AND urgency_level = $urgencyLevel");
        }

        if (!string.IsNullOrWhiteSpace(request.FromDate) && columns.Contains("issue_date"))
        {
            where.Append(" AND issue_date >= $fromDate");
        }

        if (!string.IsNullOrWhiteSpace(request.ToDate) && columns.Contains("issue_date"))
        {
            where.Append(" AND issue_date <= $toDate");
        }

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM documents {where};";
        BindSearch(countCommand, request, columns);

        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync() ?? 0);

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 100 : request.PageSize;

        var orderBy = columns.Contains("updated_at") && columns.Contains("created_at")
            ? "ORDER BY DATETIME(COALESCE(updated_at, created_at)) DESC"
            : "ORDER BY id DESC";

        using var command = connection.CreateCommand();

        command.CommandText = $@"
SELECT *
FROM documents
{where}
{orderBy}
LIMIT $pageSize OFFSET $offset;";

        BindSearch(command, request, columns);

        command.Parameters.AddWithValue("$pageSize", pageSize);
        command.Parameters.AddWithValue("$offset", (pageNumber - 1) * pageSize);

        using var reader = await command.ExecuteReaderAsync();

        var items = new List<Document>();

        while (await reader.ReadAsync())
        {
            items.Add(Map(reader));
        }

        return new PagedResult<Document>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT id, name
FROM document_categories
WHERE is_active = 1
ORDER BY name;";

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

        command.CommandText = @"
SELECT id, name
FROM document_statuses
WHERE is_active = 1
ORDER BY id;";

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

    private static async Task<HashSet<string>> GetDocumentColumnsAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText = "PRAGMA table_info(documents);";

        using var reader = await command.ExecuteReaderAsync();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            var name = reader["name"]?.ToString();

            if (!string.IsNullOrWhiteSpace(name))
            {
                columns.Add(name);
            }
        }

        return columns;
    }

    private static Dictionary<string, object?> BuildDocumentColumnValues(
        Document document,
        string now,
        bool includeId,
        bool includeCreatedAt)
    {
        var values = new Dictionary<string, object?>();

        if (includeId)
        {
            values["id"] = document.Id;
        }

        values["document_type"] = document.DocumentType;
        values["document_number"] = document.DocumentNumber;
        values["reference_number"] = document.ReferenceNumber;
        values["title"] = document.Title;
        values["summary"] = document.Summary;
        values["content_text"] = document.ContentText;
        values["issue_date"] = document.IssueDate;
        values["received_date"] = document.ReceivedDate;
        values["due_date"] = document.DueDate;
        values["sender_name"] = document.SenderName;
        values["receiver_name"] = document.ReceiverName;
        values["signer_name"] = document.SignerName;
        values["category_id"] = document.CategoryId;
        values["status_id"] = document.StatusId;
        values["confidentiality_level"] = document.ConfidentialityLevel;
        values["urgency_level"] = document.UrgencyLevel;
        values["processing_department"] = document.ProcessingDepartment;
        values["assigned_to"] = document.AssignedTo;
        values["notes"] = document.Notes;
        values["is_active"] = document.IsActive ? 1 : 0;
        values["is_expired"] = document.IsExpired ? 1 : 0;
        values["ocr_status"] = document.OcrStatus;
        values["updated_at"] = now;
        values["created_by"] = document.CreatedBy;
        values["updated_by"] = document.UpdatedBy;

        if (includeCreatedAt)
        {
            values["created_at"] = now;
        }

        return values;
    }

    private static void BindSearch(
        SqliteCommand command,
        DocumentSearchRequest request,
        HashSet<string> columns)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            command.Parameters.AddWithValue("$keyword", $"%{request.Keyword.Trim()}%");
        }

        if (request.CategoryId.HasValue && columns.Contains("category_id"))
        {
            command.Parameters.AddWithValue("$categoryId", request.CategoryId.Value);
        }

        if (request.StatusId.HasValue && columns.Contains("status_id"))
        {
            command.Parameters.AddWithValue("$statusId", request.StatusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.UrgencyLevel) && columns.Contains("urgency_level"))
        {
            command.Parameters.AddWithValue("$urgencyLevel", request.UrgencyLevel.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.FromDate) && columns.Contains("issue_date"))
        {
            command.Parameters.AddWithValue("$fromDate", request.FromDate.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.ToDate) && columns.Contains("issue_date"))
        {
            command.Parameters.AddWithValue("$toDate", request.ToDate.Trim());
        }
    }

    private static Document Map(SqliteDataReader reader)
    {
        var document = new Document();

        SetPropertyIfColumnExists(reader, document, "id", "Id");
        SetPropertyIfColumnExists(reader, document, "document_type", "DocumentType");
        SetPropertyIfColumnExists(reader, document, "document_number", "DocumentNumber");
        SetPropertyIfColumnExists(reader, document, "reference_number", "ReferenceNumber");
        SetPropertyIfColumnExists(reader, document, "title", "Title");
        SetPropertyIfColumnExists(reader, document, "summary", "Summary");
        SetPropertyIfColumnExists(reader, document, "content_text", "ContentText");
        SetPropertyIfColumnExists(reader, document, "issue_date", "IssueDate");
        SetPropertyIfColumnExists(reader, document, "received_date", "ReceivedDate");
        SetPropertyIfColumnExists(reader, document, "due_date", "DueDate");
        SetPropertyIfColumnExists(reader, document, "sender_name", "SenderName");
        SetPropertyIfColumnExists(reader, document, "receiver_name", "ReceiverName");
        SetPropertyIfColumnExists(reader, document, "signer_name", "SignerName");
        SetPropertyIfColumnExists(reader, document, "category_id", "CategoryId");
        SetPropertyIfColumnExists(reader, document, "status_id", "StatusId");
        SetPropertyIfColumnExists(reader, document, "confidentiality_level", "ConfidentialityLevel");
        SetPropertyIfColumnExists(reader, document, "urgency_level", "UrgencyLevel");
        SetPropertyIfColumnExists(reader, document, "processing_department", "ProcessingDepartment");
        SetPropertyIfColumnExists(reader, document, "assigned_to", "AssignedTo");
        SetPropertyIfColumnExists(reader, document, "notes", "Notes");
        SetPropertyIfColumnExists(reader, document, "is_active", "IsActive");
        SetPropertyIfColumnExists(reader, document, "is_expired", "IsExpired");
        SetPropertyIfColumnExists(reader, document, "ocr_status", "OcrStatus");
        SetPropertyIfColumnExists(reader, document, "created_at", "CreatedAt");
        SetPropertyIfColumnExists(reader, document, "updated_at", "UpdatedAt");
        SetPropertyIfColumnExists(reader, document, "created_by", "CreatedBy");
        SetPropertyIfColumnExists(reader, document, "updated_by", "UpdatedBy");

        return document;
    }

    private static void SetPropertyIfColumnExists(
        SqliteDataReader reader,
        object target,
        string columnName,
        string propertyName)
    {
        if (!ColumnExists(reader, columnName))
        {
            return;
        }

        var value = reader[columnName];

        if (value == DBNull.Value)
        {
            value = null;
        }

        SetPropertyIfExists(target, propertyName, value);
    }

    private static bool ColumnExists(SqliteDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static object ToDbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static void SetPropertyIfExists(object obj, string propertyName, object? value)
    {
        var prop = obj.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (prop == null)
        {
            return;
        }

        if (value == null || value == DBNull.Value)
        {
            if (!prop.PropertyType.IsValueType ||
                Nullable.GetUnderlyingType(prop.PropertyType) != null)
            {
                prop.SetValue(obj, null);
            }

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
                convertedValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(int))
            {
                convertedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(bool))
            {
                convertedValue = value is bool b
                    ? b
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
            }
            else if (targetType == typeof(DateTime))
            {
                convertedValue = value is DateTime dateTime
                    ? dateTime
                    : DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }
            else
            {
                convertedValue = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            prop.SetValue(obj, convertedValue);
        }
        catch
        {
            // Bỏ qua field lỗi để repository không làm gãy toàn hệ thống.
        }
    }
}