namespace WaaS.Persistence;

public class TenantStore(string connectionString) : ITenantStore
{
    public async Task<Tenant?> Get(string tenantName)
    {
        var sql = """
            SELECT t.id, t.name, 
                   COALESCE(jsonb_object_agg(tp.type, tp.profile_data) FILTER (WHERE tp.id IS NOT NULL), '{}'::jsonb) AS profiles
                FROM tenant t
                LEFT JOIN tenant_profile tp ON t.id = tp.tenant_id
                WHERE t.name = @TenantName
                GROUP BY t.id, t.name
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
            SELECT t.id, t.name, 
                   COALESCE(jsonb_object_agg(tp.type, tp.profile_data) FILTER (WHERE tp.id IS NOT NULL), '{}'::jsonb) AS profiles
                FROM tenant t
                LEFT JOIN tenant_profile tp ON t.id = tp.tenant_id
                WHERE t.id = @TenantId
                GROUP BY t.id, t.name
        """;

        using var connection = new NpgsqlConnection(connectionString);

        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new
        {
            TenantId = tenantId
        });
    }
}
