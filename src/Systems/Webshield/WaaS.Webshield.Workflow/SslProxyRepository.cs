using Dapper;
using Npgsql;

namespace WaaS.Webshield.Workflow;

public interface ISslProxyRepository
{
    Task<IReadOnlyList<string>> GetWebshieldNodes(short zone);
}

public sealed class SslProxyRepository(string connectionString) : ISslProxyRepository
{
    public async Task<IReadOnlyList<string>> GetWebshieldNodes(short zone)
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
