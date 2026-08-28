namespace WaaS.Persistence;

public class TenantStore(string connectionString) : ITenantStore
{
    public async Task<Tenant?> Get(string tenantName)
    {
        var sql = """
            SELECT id, name, profile_data as profile
                FROM tenant
                JOIN tenant_profile ON tenant.id = tenant_profile.tenant_id
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
            SELECT id, name, profile_data as profile
                FROM tenant
                JOIN tenant_profile ON tenant.id = tenant_profile.tenant_id
                WHERE id = @TenantId
        """;

        using var connection = new NpgsqlConnection(connectionString);

        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new
        {
            TenantId = tenantId
        });
    }
}
