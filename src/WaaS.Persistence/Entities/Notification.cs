namespace WaaS.Persistence;

public enum NotificationType
{
    Ack = 1,
    Tombstoned = 2,
    Error = 3,
    Intermediate = 4,
    Admin = 5,
    AdminLock = 6, // deprecated. replaced by change
    AdminUnlock = 7, // deprecated. replaced by change
    Change = 8,
}

public record Notification
{
    public long NotificationId { get; init; }
    public required NotificationType NotificationType { get; init; }
    public string? Payload { get; init; }
    public required string TransactionId { get; init; }
    public DateTime Created { get; init; }

    public T? GetPayload<T>()
    {
        if (Payload is null)
            return default;

        return System.Text.Json.JsonSerializer.Deserialize<T>(Payload)
            ?? throw new InvalidOperationException("Failed to deserialize payload.");
    }
}