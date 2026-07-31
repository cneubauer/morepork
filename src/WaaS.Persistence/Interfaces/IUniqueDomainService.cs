namespace WaaS.Persistence;

public interface IUniqueDomainService
{
    /// <summary>
    /// Inserts domain names for this space into the unique_domain pool table (active=false).
    /// Context is sourced from <paramref name="stackInstance"/> and <paramref name="namespace"/>.
    /// </summary>
    Task UpsertDomains(IStackInstance stackInstance, ulong systemInstanceId, short @namespace, IDomainProvider provider);

    /// <summary>
    /// Deletes unique_domain entries for this space whose domain name is NOT returned by
    /// <paramref name="provider"/>. Called on actual-state receipt to release domains removed
    /// and confirmed by the backend.
    /// </summary>
    Task DeleteRemovedDomains(IStackInstance stackInstance, ulong systemInstanceId, IDomainProvider provider);
}
