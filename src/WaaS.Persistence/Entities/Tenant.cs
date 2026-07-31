namespace WaaS.Persistence;

public class Tenant
{
    public required short Id { get; set; }
    public required string Name { get; set; }
    public TenantProfile Profile { get; set; } = new();
}