using DocumentManagement.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentManagement.Api.Health;

public sealed class SqliteHealthCheck : IHealthCheck
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteHealthCheck(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(result) == 1
                ? HealthCheckResult.Healthy("SQLite database is reachable.")
                : HealthCheckResult.Unhealthy("SQLite database returned an unexpected result.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQLite database health check failed.", ex);
        }
    }
}
