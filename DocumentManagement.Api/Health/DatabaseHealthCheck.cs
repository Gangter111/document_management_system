using DocumentManagement.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentManagement.Api.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseHealthCheck(IDbConnectionFactory connectionFactory)
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
                ? HealthCheckResult.Healthy($"{_connectionFactory.Provider} database is reachable.")
                : HealthCheckResult.Unhealthy($"{_connectionFactory.Provider} database returned an unexpected result.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_connectionFactory.Provider} database health check failed.", ex);
        }
    }
}
