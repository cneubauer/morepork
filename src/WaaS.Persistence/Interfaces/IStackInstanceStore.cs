namespace WaaS.Persistence;

/// <summary>
/// Provides read and write access to Stack Instance records.
/// </summary>
public interface IStackInstanceStore
{
    /// <summary>
    /// Retrieves the Stack Instance with the given ID. Throws if not found.
    /// </summary>
    /// <param name="stackInstanceId">The ID of the Stack Instance to retrieve.</param>
    /// <returns>The Stack Instance with the given ID, or null if not found.</returns>
    Task<IStackInstance?> Read(ulong stackInstanceId);

    /// <summary>
    /// Creates a new Stack Instance.
    /// </summary>
    /// <param name="stackInstance">The Stack Instance to create.</param>
    /// <returns>The created Stack Instance with its assigned ID.</returns>
    Task<IStackInstance> Create(IStackInstance stackInstance);

    /// <summary>
    /// Lists all Stack Instances for the given tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID to filter by.</param>
    /// <param name="offset">The number of records to skip for pagination.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <returns>A list of Stack Instances for the given tenant.</returns>
    Task<IEnumerable<IStackInstance>> List(short tenantId, int offset = 0, int limit = 1000);

    /// <summary>
    /// Updates the mutable fields of an existing Stack Instance.
    /// </summary>
    /// <param name="stackInstance">The Stack Instance with updated values. The ID must match an existing record.</param>
    Task Update(IStackInstance stackInstance);

    /// <summary>
    /// Deletes the Stack Instance with the given ID. This is a hard delete and will remove the record from the database.
    /// </summary>
    /// <param name="stackInstanceId">The ID of the Stack Instance to delete.</param>
    Task Delete(ulong stackInstanceId);

    /// <summary>
    /// Marks the Stack Instance with the given ID as deleted without removing it from the database.
    /// This is a soft delete that allows for historical record-keeping and potential recovery.
    /// </summary>
    /// <param name="stackInstanceId">The ID of the Stack Instance to mark as deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Tombstone(ulong stackInstanceId);
}
