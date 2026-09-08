namespace WaaS.Space.Classic.Workflow;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using WaaS.Common.Workflow;
using WaaS.Space.Classic.DesiredState;
public class ClassicWebspaceActivities(
    IDesiredStateStore<SharedWebspaceData> desiredStateStore,
    ISpaceMiddlewareService<SharedWebspaceData, WebspaceMiddleware.Webspace> webspaceMiddlewareService,
    ILogger<ClassicWebspaceActivities> logger
)
{
    [Activity]
    public async Task<ProcessingContext<SharedWebspaceData>> SendToTechMw(ProcessingContext<SharedWebspaceData> context)
    {
        try
        {
            var desiredState = await webspaceMiddlewareService.Publish(
                context.Tenant.Name,
                context.StackInstance,
                context.DesiredState,
                context.TransactionId
            );

            var saveResult = await desiredStateStore.Save(desiredState, desiredState.TransactionId, force: true);

            return context with
            {
                DesiredState = (DesiredState<SharedWebspaceData>)saveResult.Current,
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
        {
            logger.LogError(ex, "Permanent failure communicating with TechMW for transaction {TransactionId}", context.TransactionId);
            throw new ApplicationFailureException($"TechMW rejected request: {ex.Message}", ex, nonRetryable: true);
        }
        catch (Exception ex) when (ex is not ApplicationFailureException)
        {
            logger.LogWarning(ex, "Transient failure communicating with TechMW for transaction {TransactionId}, Temporal will retry", context.TransactionId);
            throw;
        }
    }

    [Activity]
    public async Task MarkAsApplied(string transactionId)
    {
        await desiredStateStore.MarkAsApplied(transactionId);
    }

    [Activity]
    public async Task UpdateProductDns(WaasContext<SharedWebspaceData> waasContext)
    {
        logger.LogInformation("Updating Product DNS for stack instance {StackInstanceId}", waasContext.StackInstance.Id);
        await Task.CompletedTask;
    }
}
