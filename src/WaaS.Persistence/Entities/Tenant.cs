namespace WaaS.Persistence;

public record Tenant
{
    public required short Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyDictionary<TenantProfileType, TenantProfile> Profiles { get; init; } = new Dictionary<TenantProfileType, TenantProfile>
    {
        { TenantProfileType.General, new TenantProfile() }
    };
}