using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace WaaS.Common.ViewModel;

public enum LockCategory
{
    /// <summary>
    /// The default lock type, without any additional properties.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Lock type for abuse-related locks.
    /// </summary>
    Abuse = 1,
}

/// <summary>
/// A lock resource model.
/// </summary>
public class LockInfo
{
    /// <summary>
    /// The lock id.
    /// </summary>
    /// <example>my-lock-123456789</example>
    [Required]
    [RegularExpression(@"[a-zA-Z0-9-_.]+")]
    [StringLength(maximumLength: 100, MinimumLength = 1)]
    public required string Id { get; set; }

    /// <summary>
    /// Optional description of the lock.
    /// </summary>
    /// <example>Just for fun. Haha!</example>
    [StringLength(1000)]
    public string? Reason { get; set; }

    /// <summary>
    /// If not set by the client, this property is set to the current Keystone username.
    /// </summary>
    /// <example>my-user</example>
    [StringLength(250)]
    public string? Responsible { get; set; }

    /// <summary>
    /// The category of the lock. This value also defines the structure of the <see cref="CategoryProperties"/>.
    /// </summary>
    /// <example>Default</example>
    public LockCategory Category { get; set; } = LockCategory.Default;

    /// <summary>
    /// Specific properties based on the lock category.
    /// </summary>
    public JsonElement? CategoryProperties { get; set; }

    /// <summary>
    /// The lock creation date.
    /// </summary>
    /// <example>2023-11-13T12:34:56Z</example>
    public DateTime? Created { get; set; }

    /// <summary>
    /// The user who created the lock.
    /// </summary>
    /// <example>my-user</example>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// The last modification date of the lock.
    /// </summary>
    /// <example>2023-11-14T09:12:00Z</example>
    public DateTime? Modified { get; set; }

    /// <summary>
    /// The user who last modified the lock.
    /// </summary>
    /// <example>my-user</example>
    public string? ModifiedBy { get; set; }
}
