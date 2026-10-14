namespace WaaS.Common.ViewModel;

public enum WaasNotificationType
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

public record AdminNote(string Subject, string Text);

public record WaasNotification
{
    public int Id { get; init; }
    public short TypeId { get; init; }
    public ulong StackInstanceId { get; init; }
    public ulong SystemInstanceId { get; init; }
    public NamespaceType Namespace { get; init; }
    public required string Zone { get; init; }
    public int Version { get; init; }
    public required string Tenant { get; init; }
    public bool Tombstoned { get; init; }
    public DateTime Created { get; init; }
    public AdminNote? AdminNote { get; init; }
    public int? ErrorCode { get; init; }
    public string? ErrorReason { get; init; }
    public string? TransactionId { get; init; }
}