using System.Reflection;

namespace WaaS.Persistence;

public class DesiredStateStore<TDesiredState>(string connectionString) : IDesiredStateStore<TDesiredState>
    where TDesiredState : IDesiredStateData, new()
{
    private static readonly short _namespace = typeof(TDesiredState)
        .GetCustomAttribute<DesiredStateDataAttribute>()?
        .Namespace
        ?? throw new InvalidOperationException($"Desired State '{typeof(TDesiredState).FullName}' is missing the DesiredStateDataAttribute.");

    private const string ReadSql = """
        SELECT
            stack_instance_id,
            system_instance_id,
            state_namespace AS Namespace,
            state_zone AS Zone,
            state_version AS Version,
            data,
            tenant,
            tombstoned,
            created,
            applied,
            expired
        FROM desired_state
        WHERE stack_instance_id = @StackInstanceId
          AND system_instance_id = @SystemInstanceId
          AND state_namespace = @Namespace
          AND (@Version IS NULL OR state_version = @Version)
        ORDER BY state_version DESC
        LIMIT 1;
        """;

    internal const string SaveSql = """
        INSERT INTO desired_state (
            stack_instance_id,
            system_instance_id,
            state_namespace,
            state_zone,
            state_version,
            data,
            tenant,
            tombstoned,
            created,
            applied,
            expired,
            next_check
        )
        VALUES (
            @StackInstanceId,
            @SystemInstanceId,
            @Namespace,
            @Zone,
            @Version,
            @Data::jsonb,
            @Tenant,
            @Tombstoned,
            @Created,
            @Applied,
            @Expired,
            @NextCheck
        )
        ON CONFLICT (stack_instance_id, system_instance_id, state_namespace, state_zone, state_version)
            DO UPDATE
            SET data = EXCLUDED.data
        RETURNING
            stack_instance_id,
            system_instance_id,
            state_namespace AS Namespace,
            state_zone AS Zone,
            state_version AS Version,
            data,
            tenant,
            tombstoned,
            created,
            applied,
            expired;
        """;

    public async Task<IDesiredState<TDesiredState>> Create(IStackInstance stackInstance)
    {
        const string sql = """
            WITH new_system AS (
                INSERT INTO system_instance (stack_instance_id)
                    VALUES (@StackInstanceId)
                RETURNING id
            )
            INSERT INTO desired_state (
                stack_instance_id, system_instance_id, state_namespace, state_zone, state_version,
                data, tenant, tombstoned, created, applied, expired, next_check
            )
            SELECT
                @StackInstanceId, id, @Namespace, @Zone, 0,
                @Data::jsonb, @Tenant, false, @Created, NULL, NULL, @NextCheck
            FROM new_system
            RETURNING
                stack_instance_id,
                system_instance_id,
                state_namespace AS Namespace,
                state_zone AS Zone,
                state_version AS Version,
                data,
                tenant,
                tombstoned,
                created,
                applied,
                expired;
            """;

        var initial = new DesiredState<TDesiredState>
        {
            StackInstanceId = stackInstance.Id,
            Tenant = stackInstance.TenantId,
            Zone = stackInstance.Zone,
        };

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();

        var desiredState = await connection.QuerySingleAsync<DesiredState<TDesiredState>>(sql, new
        {
            StackInstanceId = (long)initial.StackInstanceId,
            initial.Namespace,
            initial.Zone,
            initial.Data,
            initial.Tenant,
            initial.Created,
            initial.NextCheck,
        }, transaction);

        return desiredState;
    }

    public async Task<IDesiredState<TDesiredState>?> Read(ulong stackInstanceId, ulong systemInstanceId, ulong? version = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        return await connection.QuerySingleOrDefaultAsync<DesiredState<TDesiredState>>(ReadSql, new
        {
            StackInstanceId = (long)stackInstanceId,
            SystemInstanceId = (long)systemInstanceId,
            Namespace = _namespace,
            Version = (long?)version,
        });
    }
    public static async Task<IDesiredState<TDesiredState>> Save(NpgsqlConnection connection, NpgsqlTransaction transaction, IDesiredState<TDesiredState> desiredState, bool force = false)
    {
        var parameters = new
        {
            StackInstanceId = (long)desiredState.StackInstanceId,
            SystemInstanceId = (long?)desiredState.SystemInstanceId,
            desiredState.Namespace,
            desiredState.Zone,
            // If force is true, keep the same version and overwrite the data,
            // otherwise increment version and insert new row
            Version = (long)(force ? desiredState.Version : desiredState.Version + 1),
            desiredState.Data,
            desiredState.Tenant,
            desiredState.Tombstoned,
            desiredState.Created,
            desiredState.Applied,
            desiredState.Expired,
            desiredState.NextCheck,
        };

        return await connection.QuerySingleAsync<DesiredState<TDesiredState>>(SaveSql, parameters, transaction);
    }

    public async Task<IDesiredState<TDesiredState>> Save(IDesiredState<TDesiredState> desiredState, bool force = false)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        desiredState = await Save(connection, transaction, desiredState, force);

        await transaction.CommitAsync();

        return desiredState;
    }

    private const string ListSql = """
        SELECT DISTINCT ON (system_instance_id, state_zone)
            stack_instance_id, system_instance_id,
            state_namespace AS Namespace, state_zone AS Zone,
            state_version AS Version, data, tenant, tombstoned,
            created, applied, expired
        FROM desired_state
        WHERE stack_instance_id = @StackInstanceId
          AND state_namespace   = @Namespace
          AND expired IS NULL
          AND tombstoned = false
        ORDER BY system_instance_id, state_zone, state_version DESC
        OFFSET @Offset LIMIT @Limit
        """;

    public async Task<IEnumerable<IDesiredState<TDesiredState>>> List(ulong stackInstanceId, int offset, int limit)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        return await connection.QueryAsync<DesiredState<TDesiredState>>(ListSql, new
        {
            StackInstanceId = (long)stackInstanceId,
            Namespace = _namespace,
            Offset = offset,
            Limit = limit,
        });
    }

    private const string LookupByKeySql = """
        SELECT DISTINCT ON (ds.stack_instance_id, ds.system_instance_id, ds.state_namespace, ds.state_zone)
            ds.stack_instance_id,
            ds.system_instance_id,
            ds.state_namespace AS Namespace,
            ds.state_zone      AS Zone,
            ds.state_version   AS Version,
            ds.data,
            ds.tenant,
            ds.tombstoned,
            ds.created,
            ds.applied,
            ds.expired
        FROM desired_state ds
        JOIN lookup_resource lr ON
            lr.stack_instance_id  = ds.stack_instance_id
            AND lr.system_instance_id = ds.system_instance_id
            AND lr.state_namespace    = ds.state_namespace
            AND lr.state_zone         = ds.state_zone
        WHERE lr.resource_key   = @ResourceKey
          AND lr.resource_text  = @KeyValue
          AND ds.expired IS NULL
          AND ds.tombstoned = false
        ORDER BY ds.stack_instance_id, ds.system_instance_id, ds.state_namespace, ds.state_zone, ds.state_version DESC;
        """;

    private const string LookupByStackInstanceIdSql = """
        SELECT DISTINCT ON (system_instance_id, state_namespace, state_zone)
            stack_instance_id,
            system_instance_id,
            state_namespace AS Namespace,
            state_zone      AS Zone,
            state_version   AS Version,
            data,
            tenant,
            tombstoned,
            created,
            applied,
            expired
        FROM desired_state
        WHERE stack_instance_id = @StackInstanceId
          AND expired IS NULL
          AND tombstoned = false
        ORDER BY system_instance_id, state_namespace, state_zone, state_version DESC;
        """;

    private const string LookupBySystemInstanceIdSql = """
        SELECT DISTINCT ON (stack_instance_id, state_namespace, state_zone)
            stack_instance_id,
            system_instance_id,
            state_namespace AS Namespace,
            state_zone      AS Zone,
            state_version   AS Version,
            data,
            tenant,
            tombstoned,
            created,
            applied,
            expired
        FROM desired_state
        WHERE system_instance_id = @SystemInstanceId
          AND expired IS NULL
          AND tombstoned = false
        ORDER BY stack_instance_id, state_namespace, state_zone, state_version DESC;
        """;

    public async Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByKey(LookupResourceKeyType keyType, string keyValue)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        return await connection.QueryAsync<DesiredState<TDesiredState>>(LookupByKeySql, new
        {
            ResourceKey = (short)keyType,
            KeyValue = keyValue,
        });
    }

    public async Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByStackInstanceId(ulong stackInstanceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        return await connection.QueryAsync<DesiredState<TDesiredState>>(LookupByStackInstanceIdSql, new
        {
            StackInstanceId = (long)stackInstanceId,
        });
    }

    public async Task<IEnumerable<IDesiredState<TDesiredState>>> LookupBySystemInstanceId(ulong systemInstanceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        return await connection.QueryAsync<DesiredState<TDesiredState>>(LookupBySystemInstanceIdSql, new
        {
            SystemInstanceId = (long)systemInstanceId,
        });
    }

    internal static async Task SaveLookupResources(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IDesiredState<TDesiredState> state,
        IEnumerable<(LookupResourceKeyType ResourceKey, string Text)> entries)
    {
        const string deleteSql = """
            DELETE FROM lookup_resource
            WHERE stack_instance_id = @StackInstanceId
              AND system_instance_id = @SystemInstanceId
              AND state_namespace = @Namespace
              AND state_zone = @Zone;
            """;

        const string insertSql = """
            INSERT INTO lookup_resource
                (stack_instance_id, system_instance_id, state_namespace, state_zone,
                 tenant, resource_key, resource_text, resource_text_reverse)
            VALUES
                (@StackInstanceId, @SystemInstanceId, @Namespace, @Zone,
                 @Tenant, @ResourceKey, @Text, @TextReverse)
            ON CONFLICT DO NOTHING;
            """;

        var context = new
        {
            StackInstanceId = (long)state.StackInstanceId,
            SystemInstanceId = (long?)state.SystemInstanceId,
            state.Namespace,
            state.Zone,
        };

        await connection.ExecuteAsync(deleteSql, context, transaction);

        var rows = entries
            .Where(x => !string.IsNullOrEmpty(x.Text))
            .Select(x =>
            {
                var text = x.Text.Length > 255 ? x.Text[..255] : x.Text;
                var textReverse = new string([.. text.Reverse()]);
                return new
                {
                    context.StackInstanceId,
                    context.SystemInstanceId,
                    context.Namespace,
                    context.Zone,
                    Tenant = state.Tenant,
                    ResourceKey = (short)x.ResourceKey,
                    Text = text,
                    TextReverse = textReverse,
                };
            })
            .ToArray();

        if (rows.Length > 0)
            await connection.ExecuteAsync(insertSql, rows, transaction);
    }
}
