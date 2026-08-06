using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

public class PlacementTagInfo
{
    /// <summary>
    /// The desired placement tags to set. Replaces any previously set tags.
    /// </summary>
    /// <example>["shl:standard"]</example>
    public ICollection<string>? PlacementTags { get; set; }

    /// <summary>
    /// Notification to send to platform admins when placement tags are changed.
    /// </summary>
    [Required]
    public required AdminNotificationInfo Notify { get; set; }
}
