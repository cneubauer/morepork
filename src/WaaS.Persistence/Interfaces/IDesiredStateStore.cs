namespace WaaS.Persistence;

public interface IDesiredStateStore<TDesiredState>
{
    Task<NpgsqlTransaction> BeginTransaction();

    Task Lock(NpgsqlTransaction transaction, ulong stackInstanceId, ulong systemInstanceId);
    Task<IDesiredState<TDesiredState>> Create(NpgsqlTransaction transaction, IStackInstance stackInstance);
    Task<IDesiredState<TDesiredState>?> Read(NpgsqlTransaction transaction, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(NpgsqlTransaction transaction, int tenantId, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>> Save(NpgsqlTransaction transaction, IDesiredState<TDesiredState> desiredState, bool force = false);

    Task Schedule(NpgsqlTransaction transaction, string transactionId, ulong stackInstanceId, ulong systemInstanceId);
    Task Dispatched(NpgsqlTransaction transaction, string transactionId);
    
    Task<IDesiredState<TDesiredState>?> Read(int tenantId, ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IDesiredState<TDesiredState>?> Read(ulong stackInstanceId, ulong systemInstanceId, ulong? version = null);
    Task<IEnumerable<IDesiredState<TDesiredState>>> List(int tenantId, ulong stackInstanceId, int offset, int limit);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByKey(LookupResourceKeyType keyType, string keyValue);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupByStackInstanceId(ulong stackInstanceId);
    Task<IEnumerable<IDesiredState<TDesiredState>>> LookupBySystemInstanceId(ulong systemInstanceId);
}
