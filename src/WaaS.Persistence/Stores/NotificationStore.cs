namespace WaaS.Persistence;

public record NotificationFilter
{
    public long[] NotificationIds { get; init; } = [];
    public long? StartNotificationId { get; init; }
    public NotificationType[] NotificationTypes { get; init; } = [];
    public long[] StackInstanceIds { get; init; } = [];
    public long[] SystemInstanceIds { get; init; } = [];
    public short[] Namespaces { get; init; } = [];
    public short[] Zones { get; init; } = [];
    public DateTimeOffset? TimeStamp { get; init; }
    public int? Limit { get; init; }
    public string? TransactionId { get; init; }
}

public class NotificationStore(string connectionString) : INotificationStore
{
    private const string Columns = """
        notification_id AS NotificationId,
        notification_type AS NotificationType,
        tenant,
        stack_instance_id AS StackInstanceId,
        system_instance_id AS SystemInstanceId,
        state_namespace AS Namespace,
        state_zone AS Zone,
        state_version AS Version,
        tombstoned,
        payload::text AS Payload,
        transaction_id AS TransactionId,
        created
        """;

    public async Task<Notification> WriteNotification(NotificationType notificationType, IDesiredState desiredState, object? payload = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string insertSql = $"""
            INSERT INTO notifications
                (notification_type, tenant, stack_instance_id, system_instance_id, state_namespace,
                 state_zone, state_version, tombstoned, transaction_id, payload)
            VALUES
                (@NotificationType, @Tenant, @StackInstanceId, @SystemInstanceId, @Namespace,
                 @Zone, @Version, @Tombstoned, @TransactionId, @Payload::jsonb)
            RETURNING {Columns};
            """;

        return await connection.QuerySingleAsync<Notification>(insertSql, new
        {
            NotificationType = (short)notificationType,
            desiredState.Tenant,
            StackInstanceId = (long)desiredState.StackInstanceId,
            SystemInstanceId = (long)desiredState.SystemInstanceId,
            desiredState.Namespace,
            desiredState.Zone,
            Version = (long)desiredState.Version,
            desiredState.Tombstoned,
            desiredState.TransactionId,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
        });
    }

    public async Task<IEnumerable<Notification>> ListNotifications(short tenantId, string username, NotificationFilter filter)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // A username without a pointer row has not consumed anything yet and gets everything.
        // Empty arrays and NULLs mean "no filter". LIMIT NULL means no limit.
        const string sql = $"""
            SELECT {Columns}
            FROM notifications
            WHERE tenant = @TenantId
              AND notification_id > COALESCE(
                  (SELECT pointer FROM notification_pointer WHERE username = @Username), 0)
              AND (@StartNotificationId::bigint IS NULL OR notification_id >= @StartNotificationId)
              AND (cardinality(@NotificationIds) = 0 OR notification_id = ANY(@NotificationIds))
              AND (cardinality(@NotificationTypes) = 0 OR notification_type = ANY(@NotificationTypes))
              AND (cardinality(@StackInstanceIds) = 0 OR stack_instance_id = ANY(@StackInstanceIds))
              AND (cardinality(@SystemInstanceIds) = 0 OR system_instance_id = ANY(@SystemInstanceIds))
              AND (cardinality(@Namespaces) = 0 OR state_namespace = ANY(@Namespaces))
              AND (cardinality(@Zones) = 0 OR state_zone = ANY(@Zones))
              AND (@TransactionId::text IS NULL OR transaction_id = @TransactionId)
              AND (@TimeStamp::timestamptz IS NULL OR created >= @TimeStamp)
            ORDER BY notification_id
            LIMIT @Limit;
            """;

        return await connection.QueryAsync<Notification>(sql, new
        {
            TenantId = tenantId,
            Username = username,
            filter.StartNotificationId,
            filter.NotificationIds,
            NotificationTypes = filter.NotificationTypes.Select(x => (short)x).ToArray(),
            filter.StackInstanceIds,
            filter.SystemInstanceIds,
            filter.Namespaces,
            filter.Zones,
            filter.TransactionId,
            TimeStamp = filter.TimeStamp?.UtcDateTime,
            filter.Limit,
        });
    }

    public async Task<long> GetNotificationPointer(string username)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT pointer FROM notification_pointer WHERE username = @Username;";

        return await connection.ExecuteScalarAsync<long?>(sql, new { Username = username }) ?? 0;
    }

    public async Task SetNotificationPointer(string username, long notificationId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // GREATEST keeps the pointer from moving backwards on late or concurrent updates.
        const string sql = """
            INSERT INTO notification_pointer (username, pointer)
            VALUES (@Username, @NotificationId)
            ON CONFLICT (username) DO UPDATE
            SET pointer = GREATEST(notification_pointer.pointer, EXCLUDED.pointer),
                last_modified = NOW();
            """;

        await connection.ExecuteAsync(sql, new { Username = username, NotificationId = notificationId });
    }
}