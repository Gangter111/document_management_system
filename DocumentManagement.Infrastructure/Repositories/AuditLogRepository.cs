using System.Globalization;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DocumentManagement.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AuditLogRepository(SqliteConnectionFactory connectionFactory)
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
    username,
    created_at
)
VALUES
(
    $entity_name,
    $entity_id,
    $action,
    $old_values,
    $new_values,
    $username,
    $created_at
);";

        cmd.Parameters.AddWithValue("$entity_name", log.EntityName);
        cmd.Parameters.AddWithValue("$entity_id", log.EntityId);
        cmd.Parameters.AddWithValue("$action", log.Action);
        cmd.Parameters.AddWithValue("$old_values", log.OldValues ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$new_values", log.NewValues ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$username", log.Username);
        cmd.Parameters.AddWithValue("$created_at", log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

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
    username,
    created_at
FROM audit_logs
WHERE entity_name = $entity_name
  AND entity_id = $entity_id
ORDER BY id ASC;";

        cmd.Parameters.AddWithValue("$entity_name", entityName);
        cmd.Parameters.AddWithValue("$entity_id", entityId);

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
WHERE entity_name = $entity_name
  AND entity_id = $entity_id
  AND action = $action;";

        cmd.Parameters.AddWithValue("$entity_name", entityName);
        cmd.Parameters.AddWithValue("$entity_id", entityId);
        cmd.Parameters.AddWithValue("$action", action);

        var result = await cmd.ExecuteScalarAsync();

        return Convert.ToInt32(result ?? 0);
    }

    private static AuditLog Map(SqliteDataReader reader)
    {
        return new AuditLog
        {
            Id = Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
            EntityName = reader["entity_name"]?.ToString() ?? string.Empty,
            EntityId = Convert.ToInt64(reader["entity_id"], CultureInfo.InvariantCulture),
            Action = reader["action"]?.ToString() ?? string.Empty,
            OldValues = reader["old_values"] == DBNull.Value ? null : reader["old_values"]?.ToString(),
            NewValues = reader["new_values"] == DBNull.Value ? null : reader["new_values"]?.ToString(),
            Username = reader["username"]?.ToString() ?? string.Empty,
            CreatedAt = DateTime.Parse(reader["created_at"]?.ToString() ?? DateTime.MinValue.ToString("O"), CultureInfo.InvariantCulture)
        };
    }
}