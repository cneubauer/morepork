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
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="systemInstanceId" example="5001234567">The system instance identifier.</param>
    [HttpPut("{stackInstanceId}/{systemInstanceId}")]
    public async Task<IActionResult> ReceiveActualState(
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId
    )
    {
        var childWorkflowId = $"wait-notify-{stackInstanceId}-{systemInstanceId}";

        var workflowHandle = temporalClient.GetWorkflowHandle<WaitForAckWorkflow>(childWorkflowId);

        await workflowHandle.SignalAsync(
            workflow => workflow.ReceiveCompletionSignalAsync()
        );

        return Ok();
    }
}