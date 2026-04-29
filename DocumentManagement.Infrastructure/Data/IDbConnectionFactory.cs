using System.Data.Common;

namespace DocumentManagement.Infrastructure.Data;

public interface IDbConnectionFactory
{
    DatabaseProvider Provider { get; }

    DbConnection CreateConnection();
}
