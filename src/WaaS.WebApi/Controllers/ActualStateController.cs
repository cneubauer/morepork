using Microsoft.AspNetCore.Mvc;

namespace WaaS.WebApi;

[ApiController]
[Route("api/actual-state")]
public class ActualStateController(ITemporalClient temporalClient) : ControllerBase
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
        var workflowHandle = temporalClient.GetWorkflowHandle<PublishWorkflow>(resourceId);

        await workflowHandle.SignalAsync(
            workflow => workflow.ReceiveCompletionSignalAsync(transactionId)
        );

        return Ok();
    }
}