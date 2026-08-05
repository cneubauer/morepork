using Microsoft.AspNetCore.Mvc;
using Temporalio.Api.Enums.V1;
using Temporalio.Exceptions;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/stretchspaces")]
public class SharedWebspaceController(ITemporalClient temporalClient, IDesiredStateStore<SharedWebspaceData> desiredStateStore) : ControllerBase
{
    [HttpPut("{systemInstanceId}")]
    public async Task<IActionResult> UpdateSharedWebspace(
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId,
        [FromBody] Space.Classic.ViewModel.SharedWebspace webspace,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    )
    {
        transactionId ??= $"waas-update-{Guid.NewGuid()}";

        #region Update Desired State

        using var transaction = await desiredStateStore.BeginTransaction();

        await desiredStateStore.Lock(transaction, stackInstanceId, systemInstanceId);

        var desiredState = await desiredStateStore.Read(transaction, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        desiredState.Data.Webspace.Apply(webspace);

        desiredState = await desiredStateStore.Save(transaction, desiredState);

        await desiredStateStore.Schedule(transaction, transactionId, stackInstanceId, systemInstanceId);

        await transaction.CommitAsync();

        #endregion

        #region Dispath Workflow

        using var dispathTransaction = await desiredStateStore.BeginTransaction();

        await desiredStateStore.Dispatched(dispathTransaction, transactionId);

        await temporalClient.StartWorkflowAsync(
            (PublishWorkflow workflow) => workflow.RunAsync(stackInstanceId, systemInstanceId, webspace),
            new WorkflowOptions
            {
                Id = transactionId,
                TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                Rpc = new RpcOptions { CancellationToken = HttpContext.RequestAborted },
            });

        await dispathTransaction.CommitAsync();

        #endregion

        #region Await Result

        var result = await temporalClient
            .GetWorkflowHandle(transactionId)
            .GetResultAsync<WaasResult<SharedWebspaceData>>(
                rpcOptions: new RpcOptions { CancellationToken = HttpContext.RequestAborted });

        #endregion

        if (result is null)
            return Accepted(desiredState!.Data.Space.ToViewModel<Space.Classic.ViewModel.SharedWebspace>(desiredState.SystemInstanceId));

        if (result.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = result.ValidationErrors });

        return Ok(result.DesiredState!.Data.Space.ToViewModel<Space.Classic.ViewModel.SharedWebspace>(result.DesiredState.SystemInstanceId));
    }

    [HttpGet("{systemInstanceId}")]
    public async Task<IActionResult> ReadSharedWebspace([FromRoute] ulong stackInstanceId, [FromRoute] ulong systemInstanceId)
    {
        var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        var webspace = desiredState.Data.Space.ToViewModel<Space.Classic.ViewModel.SharedWebspace>(desiredState.SystemInstanceId);

        return Ok(webspace);
    }
}