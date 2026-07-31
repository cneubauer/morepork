using Microsoft.AspNetCore.Mvc;
using Temporalio.Client;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/stretchspaces")]
public class SharedWebspaceController(ITemporalClient temporalClient, IDesiredStateStore<Space.Classic.DesiredState.SharedWebspace> desiredStateStore) : ControllerBase
{
    // [HttpPost]
    // public async Task<IActionResult> CreateSharedWebspace(
    //     [FromRoute] ulong stackInstanceId,
    //     [FromBody] SharedWebspace webspace,
    //     [FromHeader(Name = "Transaction-Id")] string? transactionId
    // )
    // {
    //     transactionId ??= $"waas-create-{Guid.NewGuid()}";

    //     var options = new WorkflowOptions(id: transactionId, taskQueue: "default");

    //     var result = await temporalClient.ExecuteWorkflowAsync(
    //         (PublishWorkflow wf) => wf.RunAsync(stackInstanceId, webspace),
    //         options
    //     );

    //     if (result.ValidationErrors.Count > 0)
    //         return BadRequest(new { Errors = result.ValidationErrors });

    //     return Ok(result.DesiredState);
    // }

    [HttpGet("{systemInstanceId}")]
    public async Task<IActionResult> ReadSharedWebspace([FromRoute] ulong stackInstanceId, [FromRoute] ulong systemInstanceId)
    {
        var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        var webspace = desiredState.Data.ToViewModel(desiredState.SystemInstanceId);

        return Ok(webspace);
    }
}