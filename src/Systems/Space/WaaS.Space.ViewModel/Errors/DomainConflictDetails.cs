using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public struct DomainConflict
{
    /// <summary>
    /// The conflicting domain name.
    /// </summary>
    /// <example>example.com</example>
    public string Domain { get; set; }

    /// <summary>
    /// The ID of the Stack Instance that already holds this domain.
    /// </summary>
    /// <example>1001</example>
    public ulong StackInstanceId { get; set; }

    /// <summary>
    /// The namespace in which the domain conflict occurred.
    /// </summary>
    /// <example>StretchSpace</example>
    public NamespaceType Namespace { get; set; }
}

public class DomainConflictDetails
{
    /// <summary>
    /// The list of domain conflicts detected during processing.
    /// </summary>
    public IEnumerable<DomainConflict> Conflicts { get; set; } = Enumerable.Empty<DomainConflict>();
}