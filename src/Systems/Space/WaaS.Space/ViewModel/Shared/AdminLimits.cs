using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

/// <summary>
/// Resource limits applied by a platform admin. Admin limits always take precedence over tenant-requested limits.
/// </summary>
public class AdminLimits
{
    /// <summary>
    /// The disk quota in bytes set by a platform admin.
    /// </summary>
    /// <example>10737418240</example>
    [Required]
    public required ulong DiskQuota { get; set; }
}
