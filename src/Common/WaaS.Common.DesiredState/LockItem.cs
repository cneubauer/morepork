using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaaS.Common.DesiredState;

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

public class LockItem
{
    public LockItem() { }

    public LockItem(string id, string user, string? responsible = null)
    {
        var now = DateTime.UtcNow;

        Id = id;
        Responsible = responsible ?? user;
        Created = now;
        CreatedBy = user;
        Modified = now;
        ModifiedBy = user;
    }

    /// <summary>
    /// A well defined lock type, see LockItemType.
    /// </summary>
    public LockItemType LockType { get; set; }

    /// <summary>
    /// The lock id.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Optional description of the lock.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Name of a user or a team.
    /// </summary>
    public string Responsible { get; set; } = "";

    public LockCategory Category { get; set; } = LockCategory.Default;

    /// <summary>
    /// Specific properties based on the lock category.
    /// We use a JSON string here in order to preserve the key name casing,
    /// because JSON property names are converted to lowercase when saving the Desired State.
    /// </summary>
    public JsonElement? CategoryProperties { get; set; }

    /// <summary>
    /// Time when the lock was created, just for analysis/debug purposes.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? Modified { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Indicates if the lock can be removed by the tenant.
    /// </summary>
    public bool RemovableByTenant { get; set; } = true;

    public override string ToString() => Id;
}
