namespace WaaS.Common.Workflow;

public static class DesiredStateStoreExtensions
{
    public static async Task<ProcessingContext<TDesiredState>?> Upsert<TDesiredState>(
        this IDesiredStateStore<TDesiredState> store,
        WaasContext context,
        ulong? givenSystemInstanceId,
        Action<IDesiredState<TDesiredState>> modify
    ) where TDesiredState : IDesiredStateData, new()
    {
        await using var transaction = await store.BeginTransaction();

        var systemInstanceId = givenSystemInstanceId ?? await store.CreateSystemInstanceId(transaction, context.StackInstance.Id);

        await store.Lock(transaction, context.StackInstance.Id, systemInstanceId);

        var desiredState = givenSystemInstanceId.HasValue
            ? await store.Read(transaction, context.Tenant.Id, context.StackInstance.Id, systemInstanceId)
            : DesiredState<TDesiredState>.Create(context.Tenant, context.StackInstance, systemInstanceId, context.TransactionId);

        if (desiredState is null)
            return null;

        modify(desiredState);

        var saveResult = await store.Save(transaction, desiredState, context.TransactionId);

        var desiredStateContext = new ProcessingContext<TDesiredState>()
        {
            Tenant = context.Tenant,
            StackInstance = context.StackInstance,
            DesiredState = (DesiredState<TDesiredState>)saveResult.Current,
            TransactionId = context.TransactionId,
            Changes = saveResult.Changes,
        };

        await store.AddOutboxMessage(transaction, desiredStateContext);

        await transaction.CommitAsync();

        return desiredStateContext;
    }
}
