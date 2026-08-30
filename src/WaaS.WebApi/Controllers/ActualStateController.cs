using Microsoft.AspNetCore.Mvc;

namespace WaaS.WebApi;

[ApiController]
[Route("api/actual-state")]
public class ActualStateController(ITemporalClient temporalClient, ILogger<ActualStateController> logger) : ControllerBase
{
    /// <summary>
    /// Receive Actual State
    /// </summary>
    /// <remarks>
    /// Signals the waiting workflow with the actual state reported by the backend, acknowledging a published desired state.
    /// </remarks>
    /// <param name="resourceId" example="webspace-1234567-5001234567">The resource identifier.</param>
    /// <param name="transactionId" example="123e4567-e89b-12d3-a456-426614174000">The transaction identifier, as sent to the backend as the correlation id.</param>
    [HttpPut("{resourceId}/{transactionId}")]
    public async Task<IActionResult> ReceiveActualState(
        [FromRoute] string resourceId,
        [FromRoute] string transactionId
    )
    {
        try
        {
            var workflowHandle = temporalClient.GetWorkflowHandle<PublishClassicWebspaceWorkflow>(resourceId);
            await workflowHandle.SignalAsync(
                workflow => workflow.ReceiveBackendNotification(transactionId)
            );
        }
        catch (Exception ex)
        {
            var fallbackId = $"{resourceId}-{transactionId}";
            logger.LogWarning(ex, "Could not signal workflow with resourceId {ResourceId}, trying fallback {FallbackId}", resourceId, fallbackId);
            var workflowHandle = temporalClient.GetWorkflowHandle<PublishClassicWebspaceWorkflow>(fallbackId);
            await workflowHandle.SignalAsync(
                workflow => workflow.ReceiveBackendNotification(transactionId)
            );
        }

        return Ok();
    }
}