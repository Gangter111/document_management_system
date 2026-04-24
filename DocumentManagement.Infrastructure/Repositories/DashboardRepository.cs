using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DocumentManagement.Infrastructure.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DashboardRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardSummaryModel> GetSummaryAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    COUNT(*) as total,
    SUM(CASE WHEN status_id = 1 THEN 1 ELSE 0 END) as draft,
    SUM(CASE WHEN status_id = 2 THEN 1 ELSE 0 END) as pending,
    SUM(CASE WHEN status_id = 3 THEN 1 ELSE 0 END) as issued,
    SUM(CASE WHEN status_id = 4 THEN 1 ELSE 0 END) as archived,
    SUM(CASE WHEN status_id = 5 THEN 1 ELSE 0 END) as rejected,
    SUM(CASE 
        WHEN due_date IS NOT NULL 
        AND DATE(due_date) < DATE('now') 
        AND status_id != 3 THEN 1 
        ELSE 0 
    END) as overdue
FROM documents
WHERE is_active = 1;
";

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new DashboardSummaryModel
            {
                TotalDocuments = reader.GetInt32(0),
                DraftDocuments = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                PendingApprovalDocuments = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                IssuedDocuments = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ArchivedDocuments = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                RejectedDocuments = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                OverdueDocuments = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
            };
        }

        return new DashboardSummaryModel();
    }

    public async Task<IReadOnlyList<RecentDocumentItem>> GetRecentDocumentsAsync(int top)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT 
    d.id,
    d.document_number,
    d.title,
    s.name as status_name,
    d.due_date,
    d.updated_at
FROM documents d
LEFT JOIN document_statuses s ON d.status_id = s.id
ORDER BY DATETIME(COALESCE(d.updated_at, d.created_at)) DESC
LIMIT $top;
";

        command.Parameters.AddWithValue("$top", top);

        using var reader = await command.ExecuteReaderAsync();

        var list = new List<RecentDocumentItem>();

        while (await reader.ReadAsync())
        {
            list.Add(new RecentDocumentItem
            {
                Id = reader.GetInt64(0),
                DocumentCode = reader["document_number"]?.ToString() ?? "",
                Title = reader["title"]?.ToString() ?? "",
                StatusName = reader["status_name"]?.ToString() ?? "",
                DueDate = ParseDate(reader["due_date"]),
                UpdatedAt = ParseDate(reader["updated_at"])
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<DashboardStatusChartItem>> GetStatusBreakdownAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT 
    s.name,
    COUNT(d.id)
FROM document_statuses s
LEFT JOIN documents d ON d.status_id = s.id AND d.is_active = 1
GROUP BY s.id, s.name
ORDER BY s.id;
";

        using var reader = await command.ExecuteReaderAsync();

        var list = new List<DashboardStatusChartItem>();

        while (await reader.ReadAsync())
        {
            list.Add(new DashboardStatusChartItem
            {
                Name = reader.GetString(0),
                Value = reader.GetInt32(1)
            });
        }

        return list;
    }

    private static DateTime? ParseDate(object value)
    {
        if (value == DBNull.Value || value == null)
            return null;

        if (DateTime.TryParse(value.ToString(), out var dt))
            return dt;

        return null;
    }
}