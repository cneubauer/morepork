using Temporalio.Activities;

namespace WaaS.Space.Classic.Workflow;

public class ClassicWebspaceActivities(
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

        var saveResult = await desiredStateStore.Save(desiredState, force: true);

        return waasContext with
        {
            DesiredState = (DesiredState<SharedWebspaceData>)saveResult.Current,
        };
    }

    [Activity]
    public async Task UpdateProductDns(WaasContext<SharedWebspaceData> waasContext)
    {
        
    }
}