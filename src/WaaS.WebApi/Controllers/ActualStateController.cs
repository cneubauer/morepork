using Microsoft.AspNetCore.Mvc;
using Temporalio.Client;

namespace WaaS.WebApi;

[ApiController]
[Route("api/actual-state")]
public class ActualStateController(ITemporalClient temporalClient) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> ReceiveActualState([FromBody] IDesiredState<SharedWebspaceData> desiredState)
    {
        var childWorkflowId = $"wait-notify-{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}";

        var workflowHandle = temporalClient.GetWorkflowHandle<WaitForAckWorkflow>(childWorkflowId);

        await workflowHandle.SignalAsync(
            workflow => workflow.ReceiveCompletionSignalAsync(desiredState)
        );

        return Ok();
    }
}