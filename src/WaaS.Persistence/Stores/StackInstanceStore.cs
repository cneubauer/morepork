using Dapper;
using Npgsql;

namespace WaaS.Persistence;

public class StackInstanceStore(string connectionString) : IStackInstanceStore
{
    public async  Task<IEnumerable<IStackInstance>> List(short tenantId, int offset = 0, int limit = 1000)
    {
        var sql = @"
            SELECT stack_instance_id AS Id,
                   state_tenant AS TenantId,
                   state_zone AS Zone,
                   dependency_mode AS DependencyMode,
                   created AS Created,
                   ext_reference AS ExternalReference,
                   tags AS Tags
            FROM stack_instance
            WHERE state_tenant = @TenantId
            ORDER BY created DESC
            OFFSET @Offset
            LIMIT @Limit
        ";

        using var conn = new NpgsqlConnection(connectionString);

        return await conn.QueryAsync<StackInstance>(sql, new
        {
            TenantId = tenantId, 
            Offset = offset, 
            Limit = limit
        });
    }

    public async Task<IStackInstance> Create(IStackInstance stackInstance)
    {
        var sql = @"
            INSERT INTO stack_instance (state_tenant, state_zone, dependency_mode, created, ext_reference, tags)
                VALUES(@TenantId, @Zone, @DependencyMode, @Created, @ExternalReference, @Tags)
            RETURNING stack_instance_id AS Id,
                      state_tenant AS TenantId,
                      state_zone AS Zone,
                      dependency_mode AS DependencyMode,
                      created AS Created,
                      tombstoned AS Tombstoned,
                      ext_reference AS ExternalReference,
                      tags AS Tags
            ;
        ";

        using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleAsync<StackInstance>(sql, stackInstance);
    }

    public async Task<IStackInstance?> Read(ulong stackInstanceId)
    {
        var sql = @"
            SELECT stack_instance_id AS Id,
                   state_tenant AS TenantId,
                   state_zone AS Zone,
                   dependency_mode AS DependencyMode,
                   created AS Created,
                   tombstoned AS Tombstoned,
                   ext_reference AS ExternalReference,
                   tags AS Tags
            FROM stack_instance
            WHERE stack_instance_id = @StackInstanceId
        ";

        using var conn = new NpgsqlConnection(connectionString);

        return await conn.QuerySingleOrDefaultAsync<StackInstance>(sql, new
        {
            StackInstanceId = (long)stackInstanceId
        });
    }

    public async Task Update(IStackInstance stackInstance)
    {
        var sql = @"
            UPDATE stack_instance
            SET state_zone = @Zone,
                dependency_mode = @DependencyMode,
                ext_reference = @ExternalReference,
                tags = @Tags
            WHERE stack_instance_id = @Id
        ";

        using var conn = new NpgsqlConnection(connectionString);

        await conn.ExecuteAsync(sql, new
        {
            Id = (long)stackInstance.Id,
            stackInstance.Zone,
            stackInstance.DependencyMode,
            stackInstance.ExternalReference,
            stackInstance.Tags,
        });
    }

    public async Task Delete(ulong stackInstanceId)
    {
        var sql = @"
            DELETE FROM stack_instance
            WHERE stack_instance_id = @StackInstanceId
        ";

        using var conn = new NpgsqlConnection(connectionString);

        await conn.ExecuteAsync(sql, new { StackInstanceId = (long)stackInstanceId });
    }

    public async Task Tombstone(ulong stackInstanceId)
    {
        var sql = @"
            UPDATE stack_instance
            SET tombstoned = TRUE
            WHERE stack_instance_id = @StackInstanceId
        ";

        using var conn = new NpgsqlConnection(connectionString);

        await conn.ExecuteAsync(sql, new { StackInstanceId = (long)stackInstanceId });
    }
}
