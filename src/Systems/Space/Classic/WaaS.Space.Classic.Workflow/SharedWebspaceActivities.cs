using Temporalio.Activities;

namespace WaaS.Space.Classic.Workflow;

public class SharedWebspaceActivities(
    IDesiredStateStore<SharedWebspaceData> desiredStateStore,
    ISpaceMiddlewareService<SharedWebspaceData, WebspaceMiddleware.Webspace> webspaceMiddlewareService
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

        await desiredStateStore.Save(desiredState, force: true);

        return waasContext with
        {
            DesiredState = (DesiredState<SharedWebspaceData>)desiredState,
        };
    }
}