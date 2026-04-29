using System.Data.Common;
using System.Globalization;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(AuditLog log)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
INSERT INTO audit_logs
(
    entity_name,
    entity_id,
    action,
    old_values,
    new_values,
    changed_columns,
    username,
    created_at
)
VALUES
(
    @entity_name,
    @entity_id,
    @action,
    @old_values,
    @new_values,
    @changed_columns,
    @username,
    @created_at
);";

        cmd.AddParameter("entity_name", log.EntityName);
        cmd.AddParameter("entity_id", log.EntityId);
        cmd.AddParameter("action", log.Action);
        cmd.AddParameter("old_values", log.OldValues);
        cmd.AddParameter("new_values", log.NewValues);
        cmd.AddParameter("changed_columns", log.ChangedColumns);
        cmd.AddParameter("username", log.Username);
        cmd.AddParameter("created_at", log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditLog>> GetByEntityAsync(string entityName, long entityId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
SELECT
    id,
    entity_name,
    entity_id,
    action,
    old_values,
    new_values,
    changed_columns,
    username,
    created_at
FROM audit_logs
WHERE entity_name = @entity_name
  AND entity_id = @entity_id
ORDER BY id ASC;";

        cmd.AddParameter("entity_name", entityName);
        cmd.AddParameter("entity_id", entityId);

        using var reader = await cmd.ExecuteReaderAsync();

        var result = new List<AuditLog>();

        while (await reader.ReadAsync())
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task<int> CountByEntityActionAsync(string entityName, long entityId, string action)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
SELECT COUNT(*)
FROM audit_logs
WHERE entity_name = @entity_name
  AND entity_id = @entity_id
  AND action = @action;";

        cmd.AddParameter("entity_name", entityName);
        cmd.AddParameter("entity_id", entityId);
        cmd.AddParameter("action", action);

        var result = await cmd.ExecuteScalarAsync();

        return Convert.ToInt32(result ?? 0);
    }

    private static AuditLog Map(DbDataReader reader)
    {
        return new AuditLog
        {
            Id = Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
            EntityName = reader["entity_name"]?.ToString() ?? string.Empty,
            EntityId = Convert.ToInt64(reader["entity_id"], CultureInfo.InvariantCulture),
            Action = reader["action"]?.ToString() ?? string.Empty,
            OldValues = reader["old_values"] == DBNull.Value ? null : reader["old_values"]?.ToString(),
            NewValues = reader["new_values"] == DBNull.Value ? null : reader["new_values"]?.ToString(),
            ChangedColumns = reader["changed_columns"] == DBNull.Value ? null : reader["changed_columns"]?.ToString(),
            Username = reader["username"]?.ToString() ?? string.Empty,
            CreatedAt = DateTime.Parse(reader["created_at"]?.ToString() ?? DateTime.MinValue.ToString("O"), CultureInfo.InvariantCulture)
        };
    }
}
