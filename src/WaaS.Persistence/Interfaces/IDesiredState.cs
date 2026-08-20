namespace WaaS.Persistence;

/// <summary>
/// Represents the metadata and lifecycle state of a Desired State record.
/// A Desired State describes the target configuration for a provisioned system resource
/// and is persisted as a versioned JSON document.
/// </summary>
public interface IDesiredState
{
    /// <summary>
    /// The ID of the Stack Instance this desired state belongs to.
    /// A Stack Instance groups all system resources provisioned for a single tenant product slot.
    /// </summary>
    ulong StackInstanceId { get; }

    /// <summary>
    /// The system-specific instance ID that uniquely identifies this desired state within its Stack Instance and namespace.
    /// Null for newly created desired states that have not yet been persisted.
    /// </summary>
    ulong? SystemInstanceId { get; set; }

    /// <summary>
    /// The namespace partitioning the desired state within the Stack Instance.
    /// One Stack Instance can hold multiple desired states of the same system type, differentiated by namespace.
    /// </summary>
    short Namespace { get; }

    /// <summary>
    /// The geographic/datacenter zone in which this resource is hosted.
    /// </summary>
    short Zone { get; }

    /// <summary>
    /// The monotonically increasing version number of this desired state.
    /// Each save operation produces a new version.
    /// </summary>
    ulong Version { get; }

    /// <summary>
    /// The numeric ID of the tenant that owns this desired state.
    /// </summary>
    short Tenant { get; }

    /// <summary>
    /// Indicates whether this desired state is marked for deletion.
    /// A tombstoned desired state triggers a delete operation on the backend system.
    /// </summary>
    bool Tombstoned { get; }

    /// <summary>
    /// The timestamp when this desired state was first created.
    /// </summary>
    DateTime Created { get; }

    /// <summary>
    /// The timestamp when this desired state was last successfully pushed to the backend system.
    /// Null if it has never been applied.
    /// </summary>
    DateTime? Applied { get; }

    /// <summary>
    /// The timestamp when this version was superseded by a newer version.
    /// Null if this is still the current version.
    /// </summary>
    DateTime? Expired { get; }

    /// <summary>
    /// The timestamp at which the next scheduled consistency check should be triggered.
    /// Derived from <see cref="IDesiredStateData.GetNextCheck"/>.
    /// </summary>
    DateTime? NextCheck { get; }

    /// <summary>
    /// The ID of the transaction that last modified this desired state.
    /// </summary>
    string TransactionId { get; }

    void Tombstone();
}

/// <summary>
/// Extends <see cref="IDesiredState"/> with typed access to the domain-specific resource data.
/// </summary>
/// <typeparam name="T">The type of the domain-specific desired state data.</typeparam>
public interface IDesiredState<T> : IDesiredState
{
    /// <summary>
    /// The domain-specific data representing the target configuration of the resource.
    /// </summary>
    T Data { get; }
}
