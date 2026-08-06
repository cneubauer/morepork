namespace WaaS.Persistence;

/// <summary>
/// Provides read access to tenant records.
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// Retrieves the tenant with the given name.
    /// </summary>
    /// <param name="tenantName">The unique name of the tenant.</param>
    /// <returns>The tenant.</returns>
    Task<Tenant?> Get(string tenantName);

    /// <summary>
    /// Retrieves the tenant with the given ID.
    /// </summary>
    /// <param name="tenantId">The unique ID of the tenant.</param>
    /// <returns>The tenant.</returns>
    Task<Tenant?> Read(short tenantId);
}
