namespace WaaS.Persistence;

public interface IDesiredStateStore<TDesiredState>
{
    Task<NpgsqlTransaction> BeginTransaction();

    /// <summary>
    /// Takes a transaction-scoped exclusive lock on one desired state, blocking until it is available.
    /// </summary>
    /// <remarks>
    /// Take this <em>before</em> the <c>Read</c> of a read-modify-write, not around the save alone:
    /// the point is to stop a concurrent writer landing between the read and the save, and a lock
    /// acquired after the read cannot do that. Saving without it risks a lost update. The lock is
    /// re-entrant within a transaction and released on commit or rollback.
    /// </remarks>
    Task Lock(NpgsqlTransaction transaction, ulong stackInstanceId, ulong systemInstanceId);
    Task<IDesiredState<TDesiredState>> Create(NpgsqlTransaction transaction, IStackInstance stackInstance, string transactionId);
    Task<IDesiredState<TDesiredState>?> Read(NpgsqlTransaction transaction, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(NpgsqlTransaction transaction, int tenantId, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<DesiredStateSaveResult<TDesiredState>> Save(IDesiredState<TDesiredState> desiredState, string transactionId, bool force = false);
    Task<DesiredStateSaveResult<TDesiredState>> Save(NpgsqlTransaction transaction, IDesiredState<TDesiredState> desiredState, string transactionId, bool force = false);
    Task MarkAsApplied(string transactionId);

    Task Schedule(NpgsqlTransaction transaction, string transactionId, ulong stackInstanceId, ulong systemInstanceId);
    Task Dispatched(string transactionId);
    
    Task<IDesiredState<TDesiredState>?> Read(int tenantId, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IEnumerable<IDesiredState<TDesiredState>>> List(int tenantId, ulong stackInstanceId, int offset, int limit);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByKey(LookupResourceKeyType keyType, string keyValue);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByStackInstanceId(ulong stackInstanceId);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupBySystemInstanceId(ulong systemInstanceId);
}
