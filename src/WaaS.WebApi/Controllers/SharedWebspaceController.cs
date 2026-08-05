using Microsoft.AspNetCore.Mvc;
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

        if (result.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = result.ValidationErrors });

        return Ok(result.DesiredState.Data.Space.ToViewModel<Space.Classic.ViewModel.SharedWebspace>(result.DesiredState.SystemInstanceId));
    }

    private async Task<WaasResult<SharedWebspaceData>> AwaitWorkflowResult(string transactionId)
    {
        var handle = temporalClient.GetWorkflowHandle(transactionId);

        while (true)
        {
            try
            {
                return await handle.GetResultAsync<WaasResult<SharedWebspaceData>>();
            }
            catch (RpcException exception) when (exception.Code == RpcException.StatusCode.NotFound)
            {
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