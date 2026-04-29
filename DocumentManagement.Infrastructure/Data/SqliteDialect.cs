using System.Data.Common;

namespace DocumentManagement.Infrastructure.Data;

public sealed class SqliteDialect : IDatabaseDialect
{
    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    public string IdentitySelectSql => "SELECT last_insert_rowid();";

    public string CurrentTimestampSql => "CURRENT_TIMESTAMP";

    public string DateTimeSortExpression(string expression)
    {
        return $"DATETIME({expression})";
    }

    public string ApplyPaging(string sql, string orderBy, string pageSizeParameter, string offsetParameter)
    {
        return $@"
{sql}
{orderBy}
LIMIT {pageSizeParameter} OFFSET {offsetParameter};";
    }

    public async Task<HashSet<string>> GetColumnsAsync(DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync();

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
}
