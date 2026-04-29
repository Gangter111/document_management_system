using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace DocumentManagement.Infrastructure.Data;

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    DbConnection IDbConnectionFactory.CreateConnection()
    {
        return CreateConnection();
    }
}
