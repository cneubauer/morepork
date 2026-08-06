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
    public async Task<WaasContext<SharedWebspaceData>> Read(string transactionId, ulong stackInstanceId, ulong systemInstanceId)
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
            StackInstance = stackInstance,
            Tenant = tenant,
            DesiredState = desiredState,
        };
    }

    [Activity]
    public async Task<WaasContext<SharedWebspaceData>?> Publish(WaasContext<SharedWebspaceData> waasContext)
    {
        var desiredState = await webspaceMiddlewareService.Publish(
            waasContext.Tenant.Name,
            waasContext.StackInstance,
            waasContext.DesiredState,
            waasContext.TransactionId
        );

        return waasContext with
        {
            DesiredState = desiredState,
        };
    }

    [Activity]
    public async Task SendNotification(ulong stackInstanceId, ulong systemInstanceId)
    {
        logger.LogInformation("Sending notification for stackInstanceId: {StackInstanceId}, systemInstanceId: {SystemInstanceId}", stackInstanceId, systemInstanceId);
    }
}