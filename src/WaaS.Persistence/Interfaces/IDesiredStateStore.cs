namespace WaaS.Persistence;

public interface IDesiredStateStore<TDesiredState>
{
    /// <summary>
    /// Creates a new desired state for the given Stack Instance. Generates and assigns a
    /// <c>SystemInstanceId</c> immediately so the caller receives a fully initialised locked state.
    /// </summary>
    Task<IDesiredState<TDesiredState>> Create(IStackInstance stackInstance);

    /// <summary>
    /// Reads the desired state for the given Stack Instance ID and system instance ID.
    /// If a version is provided, it reads that specific version. Otherwise, it reads the latest
    /// </summary>
    /// <param name="stackInstanceId">The ID of the Stack Instance.</param>
    /// <param name="systemInstanceId">The ID of the system instance.</param>
    /// <param name="version">The version of the desired state to read. If null, reads the latest version.</param>
    /// <returns>The desired state if found. Otherwise, null.</returns>
    Task<IDesiredState<TDesiredState>?> Read(ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);

    /// <summary>
    /// Saves the desired state. If force is true, it will overwrite the existing version.
    /// Otherwise the Desired State will be inserted as a new version.
    /// </summary>
    /// <param name="desiredState">The desired state to save.</param>
    /// <param name="force">Whether to overwrite the existing version or insert a new one.</param>
    /// <returns>The saved desired state.</returns>
    Task<IDesiredState<TDesiredState>> Save(IDesiredState<TDesiredState> desiredState, bool force = false);

    /// <summary>
    /// Lists the latest active desired states for the given Stack Instance ID, filtered by namespace.
    /// </summary>
    /// <param name="stackInstanceId">The Stack Instance ID to filter by.</param>
    /// <param name="offset">The number of records to skip for pagination.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <returns>The latest active desired states for the given Stack Instance ID.</returns>
    Task<IEnumerable<IDesiredState<TDesiredState>>> List(ulong stackInstanceId, int offset, int limit);

    /// <summary>
    /// Looks up desired states by a specific resource key.
    /// </summary>
    /// <param name="keyType"></param>
    /// <param name="keyValue"></param>
    /// <returns></returns>
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByKey(LookupResourceKeyType keyType, string keyValue);

    /// <summary>
    /// Looks up desired states by Stack Instance ID. 
    /// </summary>
    /// <param name="stackInstanceId"></param>
    /// <returns></returns>
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByStackInstanceId(ulong stackInstanceId);

    /// <summary>
    /// Looks up desired states by System Instance ID.
    /// </summary>
    /// <param name="systemInstanceId"></param>
    /// <returns></returns>
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupBySystemInstanceId(ulong systemInstanceId);
}
