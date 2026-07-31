using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

/// <summary>
/// Space related data.
/// </summary>
public class WebspaceData
{
    [Required]
    public PlatformType? Platform { get; set; }

    /// <summary>
    /// For the most tenants it is a read-only property and can be ignored.
    /// Please use the property zone of the Stack Instance.
    /// </summary>
    /// <example>europe</example>
    public string? RegionName { get; set; }

    /// <summary>
    /// Name of the host where the webspace is located.
    /// </summary>
    /// <example>infong42.server.lan</example>
    public string? Hostname { get; set; }

    /// <summary>
    /// The IPv4 address if the webspace server
    /// </summary>
    /// <example>10.88.74.97</example>
    public string? Ipv4 { get; set; }

    /// <summary>
    /// The IPv6 address if the webspace server
    /// </summary>
    /// <example>fd87:aa51:5c43:40:10:88:74:97</example>
    public string? Ipv6 { get; set; }

    /// <summary>
    /// Please see DataAccessDomains property.
    /// </summary>
    [Obsolete("Use DataAccessDomains property instead")]
    public string? DataAccessDomain { get; set; }

    /// <summary>
    /// Please see ManagedDomainBindings property.
    /// </summary>
    [Obsolete("Use ManagedDomainBindings property instead")]
    public string? HttpAccessDomain { get; set; }

    /// <summary>
    /// Please see ManagedDomainBindings property.
    /// </summary>
    [Obsolete("Use ManagedDomainBindings property instead")]
    public string? HttpAccessDomainEnv { get; set; }

    /// <summary>
    /// A list of placement tags currently set (actual state).
    /// </summary>
    /// <example>["shl:standard"]</example>
    public string[]? PlacementTags { get; set; }

    /// <summary>
    /// Space related limits currently set (actual state).
    /// </summary>
    public ActualWebspaceLimits? Limits { get; set; }

    /// <summary>
    /// Space related limits currently set (actual state).
    /// </summary>
    public class ActualWebspaceLimits
    {
        /// <summary>
        /// Quota currently set (actual state).
        /// </summary>
        /// <example>1234000</example>
        public ulong? DiskQuotaInBytes { get; set; }
    }
}

