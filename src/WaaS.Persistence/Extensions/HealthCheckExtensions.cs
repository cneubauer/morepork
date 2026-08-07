using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace WaaS.Persistence;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Registers a readiness check that opens a connection and runs SELECT 1. Tagged
    /// "ready" so liveness probes can exclude it — a database outage should stop a pod
    /// receiving traffic, not have Kubernetes restart it.
    /// </summary>
    public static IHealthChecksBuilder AddWaasDatabaseCheck(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "postgres")
    {
        return builder.AddAsyncCheck(
            name,
            async cancellationToken =>
            {
                try
                {
                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync(cancellationToken);

                    await using var command = new NpgsqlCommand("SELECT 1", connection);
                    await command.ExecuteScalarAsync(cancellationToken);

                    return HealthCheckResult.Healthy();
                }
                catch (Exception exception)
                {
                    return HealthCheckResult.Unhealthy("Cannot reach the WaaS database", exception);
                }
            },
            tags: ["ready"]
        );
    }
}
