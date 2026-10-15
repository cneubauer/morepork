namespace WaaS.Common.Workflow;

using Temporalio.Activities;
using WaaS.Common.DesiredState;
using WaaS.Common.ViewModel;
using WaaS.Persistence;

public class WaasService<TDesiredState>(
    IStackInstanceStore stackInstanceStore,
    ITenantStore tenantStore,
    IDesiredStateStore<TDesiredState> desiredStateStore,
    INotificationStore notificationStore,
    IPasswordService passwordService
) where TDesiredState : class, IDesiredStateData, new()
{
    public async Task<WaasContext?> ReadWaasContext(string tenant, ulong stackInstanceId, string transactionId)
    {
        var tenantEntity = await tenantStore.Read(tenant);

        if (tenantEntity is null)
            return null;

        var stackInstance = await stackInstanceStore.Read(stackInstanceId);

        if (stackInstance is null || stackInstance.TenantId != tenantEntity.Id)
            return null;

        return new WaasContext()
        {
            Tenant = tenantEntity,
            StackInstance = (StackInstance)stackInstance,
            TransactionId = transactionId,
        };
    }

    public async Task<IDesiredState<TDesiredState>?> ReadDesiredState(string tenant, ulong stackInstanceId, ulong systemInstanceId)
    {
        var waasContext = await ReadWaasContext(tenant, stackInstanceId, transactionId: "");

        if (waasContext is null)
            return null;

        return await desiredStateStore.Read(stackInstanceId, systemInstanceId);
    }

    public async Task<ProcessingContext<TDesiredState>?> Upsert(
        WaasContext waasContext,
        ulong? givenSystemInstanceId,
        IEnumerable<PasswordInfo> passwordInfos,
        Action<IDesiredState<TDesiredState>> modify,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await desiredStateStore.BeginTransaction();

        var systemInstanceId = givenSystemInstanceId ?? await desiredStateStore.CreateSystemInstanceId(transaction, waasContext.StackInstance.Id);

        // Note that this is an external call and must not block too long, as it holds the database connections open
        if (passwordInfos.Any())
            await passwordService.CreatePasswordTokens(waasContext.Tenant.Name, waasContext.StackInstance.Id, systemInstanceId, passwordInfos, cancellationToken);

        await desiredStateStore.Lock(transaction, waasContext.StackInstance.Id, systemInstanceId);

        var desiredState = givenSystemInstanceId.HasValue
            ? await desiredStateStore.Read(
                transaction,
                waasContext.Tenant.Id,
                waasContext.StackInstance.Id,
                systemInstanceId
            )
            : DesiredState<TDesiredState>.Create(
                waasContext.Tenant,
                waasContext.StackInstance,
                systemInstanceId,
                waasContext.TransactionId
            );

        if (desiredState is null)
            return null;

        modify(desiredState);

        var saveResult = await desiredStateStore.Save(transaction, desiredState, waasContext.TransactionId);

        var desiredStateContext = new ProcessingContext<TDesiredState>()
        {
            Tenant = waasContext.Tenant,
            StackInstance = waasContext.StackInstance,
            DesiredState = (DesiredState<TDesiredState>)saveResult.Current,
            TransactionId = waasContext.TransactionId,
            Changes = saveResult.Changes,
        };

        await desiredStateStore.AddOutboxMessage(transaction, desiredStateContext);

        await transaction.Commit();

        return desiredStateContext;
    }

    [Activity]
    public async Task CleanupPasswordTokens(WaasContext<TDesiredState> context)
    {
        if (context.DesiredState.Data is not IPasswordTokenContainer)
            return;

        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;

        using var transaction = await desiredStateStore.BeginTransaction();

        await desiredStateStore.Lock(
            transaction,
            context.StackInstance.Id,
            context.DesiredState.SystemInstanceId
        );

        var latestDesiredStates = await desiredStateStore.ListLatest(
            transaction,
            context.Tenant.Id,
            context.StackInstance.Id,
            context.DesiredState.SystemInstanceId,
            cancellationToken
        );

        var latestTokens = latestDesiredStates
            .Select(x => x.Data)
            .Cast<IPasswordTokenContainer>()
            .SelectMany(x => x.GetPasswordTokens());

        var existingTokens = await passwordService.ListPasswordTokens(
            context.Tenant.Name,
            context.StackInstance.Id,
            context.DesiredState.SystemInstanceId,
            cancellationToken
        );

        var unusedTokens = existingTokens
            .Select(x => x.Token)
            .Except(latestTokens);

        await passwordService.DeletePasswordTokens(context.Tenant.Name, unusedTokens);

        transaction.Transaction.Commit();
    }

    [Activity]
    public async Task<Persistence.Notification> SendIntermediateNotification(DesiredState<TDesiredState> desiredState)
    {
        return await notificationStore.WriteNotification(NotificationType.Intermediate, desiredState);
    }

    [Activity]
    public async Task<Persistence.Notification> SendFinalAckNotification(DesiredState<TDesiredState> desiredState)
    {
        var notificationType = desiredState.Tombstoned
            ? NotificationType.Tombstoned
            : NotificationType.Ack;

        return await notificationStore.WriteNotification(notificationType, desiredState);
    }
}