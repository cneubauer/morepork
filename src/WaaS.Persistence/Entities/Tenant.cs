namespace WaaS.Persistence;

public record Tenant
{
    public required short Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyDictionary<string, TenantProfile> Profiles { get; init; } = new Dictionary<string, TenantProfile>();
}