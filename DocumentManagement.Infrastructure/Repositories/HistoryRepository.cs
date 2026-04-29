using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Repositories;

public class HistoryRepository : IHistoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDatabaseDialect _dialect;

    public HistoryRepository(
        IDbConnectionFactory connectionFactory,
        IDatabaseDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
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

        command.CommandText = $@"
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
    @document_id,
    @action_type,
    @action_description,
    @old_value,
    @new_value,
    {_dialect.CurrentTimestampSql},
    @action_by
);";

        command.AddParameter("document_id", documentId);
        command.AddParameter("action_type", actionType);
        command.AddParameter("action_description", actionDescription);
        command.AddParameter("old_value", oldValue);
        command.AddParameter("new_value", newValue);
        command.AddParameter("action_by", actionBy);

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
WHERE document_id = @document_id
ORDER BY action_at DESC, id DESC;";

        command.AddParameter("document_id", documentId);

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
