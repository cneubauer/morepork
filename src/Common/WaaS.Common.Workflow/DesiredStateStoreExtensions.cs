namespace WaaS.Common.Workflow;

public static class DesiredStateStoreExtensions
{
    public static async Task<ProcessingContext<TDesiredState>?> Upsert<TDesiredState>(
        this IDesiredStateStore<TDesiredState> store,
        Tenant tenant,
        IStackInstance stackInstance,
        ulong systemInstanceId,
        bool isUpdate,
        string transactionId,
        Action<TDesiredState> apply
    ) where TDesiredState : IDesiredStateData, new()
    {
        await using var transaction = await store.BeginTransaction();

        await store.Lock(transaction, stackInstance.Id, systemInstanceId);

        var desiredState = isUpdate
            ? await store.Read(transaction, tenant.Id, stackInstance.Id, systemInstanceId)
            : await store.Build(tenant, stackInstance, systemInstanceId, transactionId);

        if (desiredState is null)
            return null;

        apply(desiredState.Data);

        var saveResult = await store.Save(transaction, desiredState, transactionId);

        var context = new ProcessingContext<TDesiredState>()
        {
            Tenant = tenant,
            StackInstance = stackInstance,
            DesiredState = (DesiredState<TDesiredState>)saveResult.Current,
            TransactionId = transactionId,
            Changes = saveResult.Changes,
        };

        await store.AddOutboxMessage(transaction, context);

        await transaction.CommitAsync();

        return context;
    }
}
