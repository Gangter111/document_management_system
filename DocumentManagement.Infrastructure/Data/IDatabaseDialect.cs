using System.Data.Common;

namespace DocumentManagement.Infrastructure.Data;

public interface IDatabaseDialect
{
    DatabaseProvider Provider { get; }

    string IdentitySelectSql { get; }

    string CurrentTimestampSql { get; }

    string DateTimeSortExpression(string expression);

    string ApplyPaging(string sql, string orderBy, string pageSizeParameter, string offsetParameter);

    Task<HashSet<string>> GetColumnsAsync(DbConnection connection, string tableName);
}
