namespace WaaS.Common.Workflow;

using Temporalio.Activities;

public class WaasActivities<TDesiredState>(
    IStackInstanceStore stackInstanceStore,
    ITenantStore tenantStore,
    IDesiredStateStore<TDesiredState> desiredStateStore,
    ILogger<WaasActivities<TDesiredState>> logger
)
where TDesiredState : class, IDesiredStateData, new()
{
    [Activity]
    public async Task<WaasContext<TDesiredState>> ReadWaasContext(string transactionId, ulong stackInstanceId, ulong systemInstanceId)
    {
        var stackInstance = await stackInstanceStore.Read(stackInstanceId)
            ?? throw new Exception($"Stack instance not found for stackInstanceId: {stackInstanceId}");

        var tenant = await tenantStore.Read(stackInstance.TenantId)
            ?? throw new Exception($"Tenant not found for tenantId: {stackInstance.TenantId}");

        var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId)
            ?? throw new Exception($"Desired state not found for stackInstanceId: {stackInstanceId}, systemInstanceId: {systemInstanceId}");
        
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
        logger.LogInformation("Sending notification for transactionId: {TransactionId}", transactionId);

        await Task.CompletedTask;
    }
}