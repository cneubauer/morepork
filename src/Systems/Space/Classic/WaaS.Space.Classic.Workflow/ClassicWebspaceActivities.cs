namespace WaaS.Space.Classic.Workflow;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using WaaS.Common.Workflow;
using WaaS.Space.Classic.DesiredState;
using WaaS.Webshield.Workflow;

public class ClassicWebspaceActivities(
    IDesiredStateStore<SharedWebspaceData> desiredStateStore,
    IWebshieldService webshieldMappingService,
    ISpaceMiddlewareService<SharedWebspaceData, WebspaceMiddleware.Webspace> webspaceMiddlewareService,
    ILogger<ClassicWebspaceActivities> logger
)
{
    [Activity]
    public async Task<WaasContext<SharedWebspaceData>> SendToTechMw(WaasContext<SharedWebspaceData> waasContext)
    {
        try
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
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
        {
            logger.LogError(ex, "Permanent failure communicating with TechMW for transaction {TransactionId}", waasContext.TransactionId);
            throw new ApplicationFailureException($"TechMW rejected request: {ex.Message}", ex, nonRetryable: true);
        }
        catch (Exception ex) when (ex is not ApplicationFailureException)
        {
            logger.LogWarning(ex, "Transient failure communicating with TechMW for transaction {TransactionId}, Temporal will retry", waasContext.TransactionId);
            throw;
        }
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

    private static List<WebshieldMapping> ExtractWebshieldMappings(SharedWebspace webspace)
    {
        if (webspace.Domains is null || webspace.Domains.Count == 0)
        {
            return [];
        }

        return webspace.Domains
            .Where(d => !string.IsNullOrWhiteSpace(d.DomainName))
            .Select(d => new WebshieldMapping(
                Domain: d.DomainName,
                Destination: d.TargetPath?.ToString() ?? string.Empty,
                IsEnabled: d.IsEnabled ?? true
            ))
            .ToList();
    }
}
