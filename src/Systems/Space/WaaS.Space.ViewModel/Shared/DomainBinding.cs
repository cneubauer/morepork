using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public class DomainBinding
{
    /// <summary>
    /// An internal ID, set by the backend, when the domain has been created successfully.
    /// </summary>
    public ulong? DomainId { get; set; }

    /// <summary>
    /// The domain name to bind to this webspace.
    /// </summary>
    /// <example>example.com</example>
    [Required]
    [RegularExpression(@"^[0-9a-z-]+(?:\.[0-9a-z-]+)+$(?!\n)")]
    [MaxLength(255)]
    [MinLength(3)]
    public string Domain { get; set; } = "";

    /// <summary>
    /// Whether the domain mapping is active on the backend system or not.
    /// This value can change, when other tenants bind this domain. The tenant who successfully challenges the DNS with a specific token
    /// wins and its domain will be enabled. All other domains will be disabled.
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Locks designed for tenant use cases. Platform admins can set and remove tenant locks as well.
    /// </summary>
    public List<LockInfo>? TenantLocks { get; set; }
}