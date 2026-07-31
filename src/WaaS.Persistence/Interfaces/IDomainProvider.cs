namespace WaaS.Persistence;

/// <summary>
/// Implemented by <see cref="IDesiredStateData"/> types that expose domain names
/// for unique-domain pool tracking. Usable by Space, Webshield, Redirects, and other systems.
/// </summary>
public interface IDomainProvider
{
    IEnumerable<string> GetDomainNames();
}
