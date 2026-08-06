namespace WaaS.Persistence;

public class TenantStore(string connectionString) : ITenantStore
{
    public async Task<Tenant?> Get(string tenantName)
    {
        var sql = """
            SELECT id, name, profile
                FROM tenant
                WHERE name = @TenantName
        """;

        using var connection = new NpgsqlConnection(connectionString);

        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new
        {
            TenantName = tenantName
        });
    }

    public async Task<Tenant?> Read(short tenantId)
    {
        var sql = """
            SELECT id, name, profile
                FROM tenant
                WHERE id = @TenantId
        """;

        using var connection = new NpgsqlConnection(connectionString);

        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new
        {
            TenantId = tenantId
        });
    }
}
