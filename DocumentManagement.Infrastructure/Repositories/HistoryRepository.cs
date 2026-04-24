using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Repositories;

public class HistoryRepository : IHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public HistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        long? documentId,
        string actionType,
        string? actionDescription,
        string? oldValue,
        string? newValue,
        string? actionBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        command.CommandText = @"
INSERT INTO document_history
(
    document_id,
    action_type,
    action_description,
    old_value,
    new_value,
    action_at,
    action_by
)
VALUES
(
    $document_id,
    $action_type,
    $action_description,
    $old_value,
    $new_value,
    CURRENT_TIMESTAMP,
    $action_by
);";

        command.Parameters.AddWithValue("$document_id", (object?)documentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action_type", actionType);
        command.Parameters.AddWithValue("$action_description", (object?)actionDescription ?? DBNull.Value);
        command.Parameters.AddWithValue("$old_value", (object?)oldValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$new_value", (object?)newValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$action_by", (object?)actionBy ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<DocumentHistoryModel>> GetByDocumentIdAsync(long documentId)
    {
        var items = new List<DocumentHistoryModel>();

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT
    id,
    document_id,
    action_type,
    action_description,
    old_value,
    new_value,
    action_at,
    action_by
FROM document_history
WHERE document_id = $document_id
ORDER BY action_at DESC, id DESC;";

        command.Parameters.AddWithValue("$document_id", documentId);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new DocumentHistoryModel
            {
                Id = reader.GetInt64(0),
                DocumentId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                ActionType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ActionDescription = reader.IsDBNull(3) ? null : reader.GetString(3),
                OldValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                NewValue = reader.IsDBNull(5) ? null : reader.GetString(5),
                ActionAt = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                ActionBy = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return items;
    }
}