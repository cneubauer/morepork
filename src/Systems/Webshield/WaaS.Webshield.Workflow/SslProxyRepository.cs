using Dapper;
using Npgsql;

namespace WaaS.Webshield.Management;

public interface ISslProxyRepository
{
    Task<IReadOnlyList<string>> GetHostnamesByZone(short zone, CancellationToken cancellationToken);
}

public sealed class SslProxyRepository(string connectionString) : ISslProxyRepository
{
    public async Task<IReadOnlyList<string>> GetHostnamesByZone(short zone, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        var hostnames = await connection.QueryAsync<string>("""
            SELECT hostname
            FROM ssl_proxy
            WHERE active = true AND zone = @zone
            ORDER BY hostname
            """,
            new { zone }
        );

        return [.. hostnames];
    }
}
