namespace WaaS.Persistence;

/// <summary>
/// Represents a Stack Instance — a container that groups all provisioned system resources
/// (webspace, database, etc.) for a single tenant product slot.
/// </summary>
public interface IStackInstance
{
    /// <summary>
    /// The unique identifier of this Stack Instance.
    /// </summary>
    ulong Id { get; }

    /// <summary>
    /// The numeric ID of the tenant that owns this Stack Instance.
    /// </summary>
    short TenantId { get; }

    /// <summary>
    /// The geographic/datacenter zone in which this Stack Instance's resources are hosted.
    /// </summary>
    short Zone { get; }

    /// <summary>
    /// Indicates whether this Stack Instance is marked for deletion.
    /// A tombstoned Stack Instance triggers deletion of all its associated desired states.
    /// </summary>
    bool Tombstoned { get; }

    /// <summary>
    /// Controls automatic dependency management between systems within this Stack Instance.
    /// For example, if a webspace depends on Webshield, the Webshield mapping is provisioned
    /// and maintained automatically without requiring explicit client configuration.
    /// </summary>
    short DependencyMode { get; }

    /// <summary>
    /// The timestamp when this Stack Instance was created.
    /// </summary>
    DateTime Created { get; }

    /// <summary>
    /// An optional external reference identifier set by the client system that created this Stack Instance.
    /// </summary>
    string? ExternalReference { get; set; }

    /// <summary>
    /// An optional list of Stack Instance tags passed through to the backend system.
    /// Max count of tags: 10. Max length of a tag: 20 characters.
    /// Allowed characters: [a-zA-Z0-9_]*
    /// </summary>
    string[]? Tags { get; set; }
}
