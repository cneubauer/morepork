namespace WaaS.Common.Workflow;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using WaaS.Common.DesiredState;
using WaaS.Persistence;

public class WaasActivities<TDesiredState>(
    IStackInstanceStore stackInstanceStore,
    ITenantStore tenantStore,
    IDesiredStateStore<TDesiredState> desiredStateStore,
    PasswordService passwordService,
    ILogger<WaasActivities<TDesiredState>> logger
) where TDesiredState : class, IDesiredStateData, new()
{
    [Activity]
    public async Task<WaasContext<TDesiredState>> ReadWaasContext(string transactionId, ulong stackInstanceId, ulong systemInstanceId, CancellationToken cancellationToken)
    {
        var stackInstance = await stackInstanceStore.Read(stackInstanceId, cancellationToken)
            ?? throw new ApplicationFailureException($"Stack instance not found for stackInstanceId: {stackInstanceId}", nonRetryable: true);

        var tenant = await tenantStore.Read(stackInstance.TenantId, cancellationToken)
            ?? throw new ApplicationFailureException($"Tenant not found for tenantId: {stackInstance.TenantId}", nonRetryable: true);

        var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId, cancellationToken: cancellationToken)
            ?? throw new ApplicationFailureException($"Desired state not found for stackInstanceId: {stackInstanceId}, systemInstanceId: {systemInstanceId}", nonRetryable: true);

        return new WaasContext<TDesiredState>
        {
            TransactionId = transactionId,
            ValidationErrors = [],
            StackInstance = (StackInstance)stackInstance,
            Tenant = tenant,
            DesiredState = (DesiredState<TDesiredState>)desiredState,
        };
    }

    [Activity]
    public async Task CleanupPasswordTokens(WaasContext<TDesiredState> context, CancellationToken cancellationToken)
    {
        if (context.DesiredState.Data is not IPasswordTokenContainer)
            return;

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

        transaction.Commit();
    }

    [Activity]
    public async Task SendIntermediateNotification(string transactionId)
    {
        logger.LogInformation("Emitting completion notification for transaction {TransactionId}", transactionId);
        await Task.CompletedTask;
    }

    [Activity]
    public async Task SendFinalAckNotification(string transactionId)
    {
        logger.LogInformation("Emitting completion notification for transaction {TransactionId}", transactionId);
        await Task.CompletedTask;
    }
}