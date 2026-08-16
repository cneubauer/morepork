using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using WaaS.Webshield.DesiredState;
using WaaS.Webshield.Workflow;

namespace WaaS.Space.Classic.Workflow;

public class ClassicWebspaceActivities(
    IDesiredStateStore<SharedWebspaceData> desiredStateStore,
    IWebshieldMappingService webshieldMappingService,
    ISpaceMiddlewareService<SharedWebspaceData, WebspaceMiddleware.Webspace> webspaceMiddlewareService,
    ILogger<ClassicWebspaceActivities> logger
)
{
    [Activity]
    public async Task<WaasContext<SharedWebspaceData>> SendToTechMw(WaasContext<SharedWebspaceData> waasContext)
    {
        var desiredState = await webspaceMiddlewareService.Publish(
            waasContext.Tenant.Name,
            waasContext.StackInstance,
            waasContext.DesiredState,
            waasContext.TransactionId
        );

        var saveResult = await desiredStateStore.Save(desiredState, desiredState.TransactionId, force: true);

        return waasContext with
        {
            DesiredState = (DesiredState<SharedWebspaceData>)saveResult.Current,
        };
    }

    [Activity]
    public async Task MarkAsApplied(WaasContext<SharedWebspaceData> waasContext)
    {
        await desiredStateStore.MarkAsApplied(waasContext.TransactionId);
    }

    [Activity]
    public async Task UpdateProductDns(WaasContext<SharedWebspaceData> waasContext)
    {
        logger.LogInformation("Updating Product DNS for stack instance {StackInstanceId}", waasContext.StackInstance.Id);
        await Task.CompletedTask;
    }
}