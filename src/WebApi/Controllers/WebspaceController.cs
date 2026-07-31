using Microsoft.AspNetCore.Mvc;
using Temporalio.Client;

namespace MyNamespace;

[ApiController]
[Route("api/{stackInstanceId}/webspaces")]
public class WebspaceController(ITemporalClient temporalClient) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateWebspace([FromRoute] ulong stackInstanceId, [FromBody] Webspace webspace)
    {
        var workflowId = $"waas-{Guid.NewGuid()}";

        var options = new WorkflowOptions(id: workflowId, taskQueue: "default");

        var result = await temporalClient.ExecuteWorkflowAsync(
            (PublishWorkflow wf) => wf.RunAsync(stackInstanceId, webspace),
            options
        );

        if (result.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = result.ValidationErrors });

        return Ok(result.DesiredState);
    }

    [HttpGet("{systemInstanceId}")]
    public async Task<IActionResult> ReadWebspace([FromRoute] ulong stackInstanceId, [FromRoute] ulong systemInstanceId)
    {
        var workflowId = $"read-{stackInstanceId}-{systemInstanceId}-{Guid.NewGuid()}";

        var options = new WorkflowOptions(id: workflowId, taskQueue: "default");

        var desiredState = await temporalClient.ExecuteWorkflowAsync(
            (ReadWorkflow wf) => wf.RunAsync(stackInstanceId, systemInstanceId),
            options
        );

        if (desiredState is null)
            return NotFound();

        return Ok(desiredState.Webspace);
    }
}