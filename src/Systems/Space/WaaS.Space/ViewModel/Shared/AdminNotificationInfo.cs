using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

public class AdminNotificationInfo
{
    /// <summary>
    /// Title of the admin notification.
    /// </summary>
    /// <example>Placement tags changed</example>
    [Required]
    [StringLength(100)]
    public required string Subject { get; set; }

    /// <summary>
    /// Description of the admin notification.
    /// </summary>
    /// <example>Disk quota limit reached for tenant dev_whic</example>
    [Required]
    [StringLength(512)]
    public required string Text { get; set; }
}
