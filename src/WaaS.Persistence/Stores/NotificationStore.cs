namespace WaaS.Persistence;

public class NotificationStore(string connectionString) : INotificationStore
{

    public async Task<Notification> WriteNotification(NotificationType notificationType, string transactionId, object? payload = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var notification = new Notification
        {
            NotificationType = notificationType,
            TransactionId = transactionId,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
        };

        const string insertSql = """
            INSERT INTO notifications
                (notification_type, transaction_id, payload)
            VALUES
                (@NotificationType, @TransactionId, @Payload::jsonb)
            RETURNING notification_id, notification_type, transaction_id, payload::text AS payload, created;
            """;

        notification = await connection.QuerySingleAsync<Notification>(insertSql, notification);

        return notification;
    }

    public async Task<IEnumerable<Notification>> ListNotifications(string userName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // A username without a pointer row has not consumed anything yet and gets everything.
        const string sql = """
            SELECT notification_id, notification_type, transaction_id, payload::text AS payload, created
            FROM notifications
            WHERE notification_id > COALESCE(
                (SELECT pointer FROM notification_pointer WHERE username = @UserName), 0)
            ORDER BY notification_id;
            """;

        return await connection.QueryAsync<Notification>(sql, new { UserName = userName });
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