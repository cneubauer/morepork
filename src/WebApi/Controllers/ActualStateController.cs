using Microsoft.AspNetCore.Mvc;
using Temporalio.Client;

namespace MyNamespace;

[ApiController]
[Route("api/actual-state")]
public class ActualStateController(ITemporalClient temporalClient) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> ReceiveActualState([FromBody] DesiredState desiredState)
    {
        var childWorkflowId = $"wait-notify-{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}";

        try
        {
            var workflowHandle = temporalClient.GetWorkflowHandle<WaitForAckWorkflow>(childWorkflowId);

            await workflowHandle.SignalAsync(
                wf => wf.ReceiveCompletionSignalAsync(desiredState)
            );

            return Ok(new { Message = "Signal and data delivered to workflow." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = "Failed to send signal", Details = ex.Message });
        }
    }
}