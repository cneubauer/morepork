using Microsoft.AspNetCore.Mvc;
using Temporalio.Client;

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
    /// <param name="desiredState">The actual state as reported by the backend.</param>
    [HttpPut]
    public async Task<IActionResult> ReceiveActualState([FromBody] DesiredState<SharedWebspaceData> desiredState)
    {
        var childWorkflowId = $"wait-notify-{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}";

        var workflowHandle = temporalClient.GetWorkflowHandle<WaitForAckWorkflow>(childWorkflowId);

        await workflowHandle.SignalAsync(
            workflow => workflow.ReceiveCompletionSignalAsync(desiredState)
        );

        return Ok();
    }
}