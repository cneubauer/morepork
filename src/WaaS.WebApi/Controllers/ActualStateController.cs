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
    /// <param name="transactionId" example="123e4567-e89b-12d3-a456-426614174000">The transaction identifier.</param>
    [HttpPut("{transactionId}")]
    public async Task<IActionResult> ReceiveActualState([FromRoute] string transactionId)
    {
        var childWorkflowId = $"{transactionId}-await-notify";

        var workflowHandle = temporalClient.GetWorkflowHandle<WaitForAckWorkflow>(childWorkflowId);

        await workflowHandle.SignalAsync(
            workflow => workflow.ReceiveCompletionSignalAsync()
        );

        return Ok();
    }
}