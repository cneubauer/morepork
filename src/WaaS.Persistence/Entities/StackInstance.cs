namespace WaaS.Persistence;

public class StackInstance : IStackInstance
{
    public ulong Id { get; init; }
    public short TenantId { get; init; }
    public short Zone { get; init; }
    public short DependencyMode { get; init; } = 3;
    public DateTime Created { get; init; } = DateTime.UtcNow;
    public bool Tombstoned { get; set; } = false;
    public string? ExternalReference { get; set; }

    /// <summary>
    /// An optional list of Stack Instance tags.
    /// Max count of tags: 10. Max length of a tag: 20 characters.
    /// Allowed character by tags: [a-zA-Z0-9_]*
    /// </summary>
    public string[]? Tags { get; set; }

    public override string ToString()
    {
        return $"<StackInstance{{Id={Id};Tenant={TenantId};Zone={Zone};ExtRef={ExternalReference};Tags={string.Join(',', Tags ?? [])}}}>";
    }
}
