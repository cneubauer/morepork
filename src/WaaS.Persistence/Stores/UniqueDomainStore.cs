namespace WaaS.Persistence;

public class UniqueDomainStore(string connectionString) : IUniqueDomainService
{
    private const string UpsertSql = """
        INSERT INTO unique_domain
            (stack_instance_id, system_instance_id, tenant, state_namespace, state_zone, domain_name, active)
        VALUES
            (@StackInstanceId, @SystemInstanceId, @Tenant, @Namespace, @Zone, @DomainName, false)
        ON CONFLICT (tenant, state_namespace, state_zone, domain_name) DO NOTHING;
        """;

    private const string DeleteSql = """
        DELETE FROM unique_domain
        WHERE stack_instance_id = @StackInstanceId
          AND system_instance_id = @SystemInstanceId
          AND domain_name NOT IN @CurrentDomainNames;
        """;

    private const string DeleteAllSql = """
        DELETE FROM unique_domain
        WHERE stack_instance_id = @StackInstanceId
          AND system_instance_id = @SystemInstanceId;
        """;

    public async Task UpsertDomains(IStackInstance stackInstance, ulong systemInstanceId, short @namespace, IDomainProvider provider)
    {
        var domainNames = provider.GetDomainNames().ToList();
        if (domainNames.Count == 0) return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var rows = domainNames.Select(x => new
        {
            StackInstanceId = (long)stackInstance.Id,
            SystemInstanceId = (long)systemInstanceId,
            Tenant = stackInstance.TenantId,
            Namespace = @namespace,
            Zone = stackInstance.Zone,
            DomainName = x,
        });

        await connection.ExecuteAsync(UpsertSql, rows);
    }

    public async Task DeleteRemovedDomains(IStackInstance stackInstance, ulong systemInstanceId, IDomainProvider provider)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var currentDomainNames = provider.GetDomainNames().ToList();

        if (currentDomainNames.Count == 0)
        {
            await connection.ExecuteAsync(DeleteAllSql, new
            {
                StackInstanceId = (long)stackInstance.Id,
                SystemInstanceId = (long)systemInstanceId,
            });
        }
        else
        {
            await connection.ExecuteAsync(DeleteSql, new
            {
                StackInstanceId = (long)stackInstance.Id,
                SystemInstanceId = (long)systemInstanceId,
                CurrentDomainNames = currentDomainNames,
            });
        }
    }
}
