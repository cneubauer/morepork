namespace WaaS.Persistence;

public interface IDesiredStateStore<TDesiredState> where TDesiredState : IDesiredStateData, new()
{
    Task<DatabaseTransaction> BeginTransaction();

    /// <summary>
    /// Takes a transaction-scoped exclusive lock on one desired state, blocking until it is available.
    /// </summary>
    /// <remarks>
    /// Take this <em>before</em> the <c>Read</c> of a read-modify-write, not around the save alone:
    /// the point is to stop a concurrent writer landing between the read and the save, and a lock
    /// acquired after the read cannot do that. Saving without it risks a lost update. The lock is
    /// re-entrant within a transaction and released on commit or rollback.
    /// </remarks>
    Task Lock(DatabaseTransaction transaction, ulong stackInstanceId, ulong systemInstanceId);
    Task<ulong> CreateSystemInstanceId(DatabaseTransaction transaction, ulong stackInstanceId);
    Task<IDesiredState<TDesiredState>?> Read(DatabaseTransaction transaction, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(DatabaseTransaction transaction, short tenantId, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(int tenantId, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<DesiredStateSaveResult<TDesiredState>> Save(IDesiredState<TDesiredState> desiredState, string transactionId, bool force = false);
    Task<DesiredStateSaveResult<TDesiredState>> Save(DatabaseTransaction transaction, IDesiredState<TDesiredState> desiredState, string transactionId, bool force = false);
    Task MarkAsApplied(string transactionId);

    Task AddOutboxMessage<TContext>(DatabaseTransaction transaction, TContext context) where TContext : notnull;
    Task RemoveOutboxMessage(string transactionId);
    
    Task<IEnumerable<IDesiredState<TDesiredState>>> List(int tenantId, ulong stackInstanceId, int offset, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists latest applied Desired State and newer ones
    /// </summary>
    /// <param name="tenantId"></param>
    /// <param name="stackInstanceId"></param>
    /// <param name="systemInstanceId"></param>
    /// <param name="offset"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    Task<IEnumerable<IDesiredState<TDesiredState>>> ListLatest(DatabaseTransaction transaction, int tenantId, ulong stackInstanceId, ulong systemInstanceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByKey(LookupResourceKeyType keyType, string keyValue, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByStackInstanceId(ulong stackInstanceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupBySystemInstanceId(ulong systemInstanceId, CancellationToken cancellationToken = default);
}
