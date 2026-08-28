namespace WaaS.Common.Workflow;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using WaaS.Persistence;

public class WaasActivities<TDesiredState>(
    IStackInstanceStore stackInstanceStore,
    ITenantStore tenantStore,
    IDesiredStateStore<TDesiredState> desiredStateStore,
    ILogger<WaasActivities<TDesiredState>> logger
) where TDesiredState : class, IDesiredStateData, new()
{
    [Activity]
    public async Task<WaasContext<TDesiredState>> ReadWaasContext(string transactionId, ulong stackInstanceId, ulong systemInstanceId)
    {
        var stackInstance = await stackInstanceStore.Read(stackInstanceId)
            ?? throw new ApplicationFailureException($"Stack instance not found for stackInstanceId: {stackInstanceId}", nonRetryable: true);

        var tenant = await tenantStore.Read(stackInstance.TenantId)
            ?? throw new ApplicationFailureException($"Tenant not found for tenantId: {stackInstance.TenantId}", nonRetryable: true);

        var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId)
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
    public async Task SendNotification(string transactionId)
    {
        logger.LogInformation("Emitting completion notification for transaction {TransactionId}", transactionId);
        await Task.CompletedTask;
    }
}