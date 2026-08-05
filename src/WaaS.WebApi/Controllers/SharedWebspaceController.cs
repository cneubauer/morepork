using Microsoft.AspNetCore.Mvc;
using Temporalio.Exceptions;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/stretchspaces")]
public class SharedWebspaceController(ITemporalClient temporalClient, IDesiredStateStore<SharedWebspaceData> desiredStateStore) : ControllerBase
{
    /// <summary>
    /// How long to wait for the executor to start the workflow. Reaching this means the executor
    /// is down or badly backed up, not that the request was invalid.
    /// </summary>
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(10);

    [HttpPut("{systemInstanceId}")]
    public async Task<IActionResult> UpdateSharedWebspace(
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId,
        [FromBody] Space.Classic.ViewModel.SharedWebspace webspace,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    )
    {
        transactionId ??= $"waas-update-{Guid.NewGuid()}";

        #region Transaction

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

        var result = await AwaitWorkflowResult(transactionId);

        if (result is null)
            return Accepted(desiredState!.Data.Space.ToViewModel<Space.Classic.ViewModel.SharedWebspace>(desiredState.SystemInstanceId));

        if (result.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = result.ValidationErrors });

        return Ok(result.DesiredState!.Data.Space.ToViewModel<Space.Classic.ViewModel.SharedWebspace>(result.DesiredState.SystemInstanceId));
    }

    /// <summary>
    /// Waits for the workflow the executor will start for this transaction.
    /// </summary>
    /// <remarks>
    /// The executor dispatches the outbox on a tick, so the workflow does not exist for a moment
    /// after the commit and NotFound simply means "not yet". Polling for that is a placeholder:
    /// the real fix is for whoever starts the workflow to also be the one awaiting it, which is a
    /// decision to make if Temporal is adopted. See plans/2026-08-05-workflow-result-await.md.
    /// </remarks>
    /// <returns>The workflow result, or null if it did not start within the timeout.</returns>
    private async Task<WaasResult<SharedWebspaceData>?> AwaitWorkflowResult(string transactionId)
    {
        var handle = temporalClient.GetWorkflowHandle(transactionId);
        var rpcOptions = new RpcOptions { CancellationToken = HttpContext.RequestAborted };
        var deadline = DateTime.UtcNow + _startTimeout;

        while (true)
        {
            try
            {
                return await handle.GetResultAsync<WaasResult<SharedWebspaceData>>(rpcOptions: rpcOptions);
            }
            catch (RpcException exception) when (exception.Code == RpcException.StatusCode.NotFound)
            {
                if (DateTime.UtcNow >= deadline)
                    return null;

                await Task.Delay(TimeSpan.FromMilliseconds(200), HttpContext.RequestAborted);
            }
        }
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