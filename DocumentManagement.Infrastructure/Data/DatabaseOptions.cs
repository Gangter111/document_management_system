namespace DocumentManagement.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "Sqlite";

    public string Path { get; set; } = "database/app.db";

    public string ConnectionString { get; set; } = string.Empty;

    public DatabaseProvider GetProvider()
    {
        return string.Equals(Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
            ? DatabaseProvider.SqlServer
            : DatabaseProvider.Sqlite;
    }
}
