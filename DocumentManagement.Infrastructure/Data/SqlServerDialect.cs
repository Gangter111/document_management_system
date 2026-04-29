using System.Data.Common;

namespace DocumentManagement.Infrastructure.Data;

public sealed class SqlServerDialect : IDatabaseDialect
{
    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public string IdentitySelectSql => "SELECT CAST(SCOPE_IDENTITY() AS bigint);";

    public string CurrentTimestampSql => "SYSUTCDATETIME()";

    public string DateTimeSortExpression(string expression)
    {
        return $"TRY_CONVERT(datetime2, {expression})";
    }

    public string ApplyPaging(string sql, string orderBy, string pageSizeParameter, string offsetParameter)
    {
        return $@"
{sql}
{orderBy}
OFFSET {offsetParameter} ROWS FETCH NEXT {pageSizeParameter} ROWS ONLY;";
    }

    public async Task<HashSet<string>> GetColumnsAsync(DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @tableName;";
        command.AddParameter("@tableName", tableName);

        await using var reader = await command.ExecuteReaderAsync();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            var name = reader["COLUMN_NAME"]?.ToString();

            if (!string.IsNullOrWhiteSpace(name))
            {
                columns.Add(name);
            }
        }

        return columns;
    }
}
