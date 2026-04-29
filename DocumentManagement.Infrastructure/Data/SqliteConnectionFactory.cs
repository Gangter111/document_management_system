using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace DocumentManagement.Infrastructure.Data;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    DbConnection IDbConnectionFactory.CreateConnection()
    {
        return CreateConnection();
    }
}
