namespace WaaS.Space.Classic.Workflow;

using WaaS.Space.Workflow;

using Temporalio.Activities;


public class SharedWebspaceActivities(
    IStackInstanceStore stackInstanceStore,
    ITenantStore tenantStore,
    IDesiredStateStore<SharedWebspaceData> desiredStateStore,
    ISpaceMiddlewareService<SharedWebspaceData, WebspaceMiddleware.Webspace> webspaceMiddlewareService,
    ILogger<SharedWebspaceActivities> logger
)
{
    [Activity]
    public async Task<WaasContext<SharedWebspaceData>> ReadWaasContext(string transactionId, ulong stackInstanceId, ulong systemInstanceId)
    {
        var stackInstance = await stackInstanceStore.Read(stackInstanceId)
            ?? throw new Exception($"Stack instance not found for stackInstanceId: {stackInstanceId}");

        var tenant = await tenantStore.Read(stackInstance.TenantId)
            ?? throw new Exception($"Tenant not found for tenantId: {stackInstance.TenantId}");

        var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId)
            ?? throw new Exception($"Desired state not found for stackInstanceId: {stackInstanceId}, systemInstanceId: {systemInstanceId}");
        
        return new WaasContext<SharedWebspaceData>
        {
            TransactionId = transactionId,
            ValidationErrors = [],
            StackInstance = (StackInstance)stackInstance,
            Tenant = tenant,
            DesiredState = (DesiredState<SharedWebspaceData>)desiredState,
        };
    }

    [Activity]
    public async Task<WaasContext<SharedWebspaceData>> SendToBackend(WaasContext<SharedWebspaceData> waasContext)
    {
        var desiredState = await webspaceMiddlewareService.Publish(
            waasContext.Tenant.Name,
            waasContext.StackInstance,
            waasContext.DesiredState,
            waasContext.TransactionId
        );

        await desiredStateStore.Save(desiredState, force: true);

        return waasContext with
        {
            DesiredState = (DesiredState<SharedWebspaceData>)desiredState,
        };
    }

    [Activity]
    public async Task SendNotification(string transactionId)
    {
        logger.LogInformation("Sending notification for transactionId: {TransactionId}", transactionId);

        await Task.CompletedTask;
    }
}