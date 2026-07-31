using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

public class AutoQuotaInfo
{
    /// <summary>
    /// The name should exist in the tenant profile.
    /// </summary>
    /// <example>quota-profile-m</example>
    [Required]
    public required string DiskQuotaProfile { get; set; }

    /// <summary>
    /// The min allowed quota in bytes which can be set by the tenant on a single resource.
    /// </summary>
    /// <example>1073741824</example>
    [Required]
    public required ulong MinDiskQuota { get; set; }

    /// <summary>
    /// The max allowed quota in bytes which can be set by the tenant on a single resource.
    /// </summary>
    /// <example>107374182400</example>
    [Required]
    public required ulong MaxDiskQuota { get; set; }

    /// <summary>
    /// A timestamp until the next possible automatic quota change.
    /// </summary>
    /// <example>2024-06-15T10:00:00Z</example>
    [Required]
    public required DateTime NextEvalNotBefore { get; set; }
}
