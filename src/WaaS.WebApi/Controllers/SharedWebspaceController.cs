using Microsoft.AspNetCore.Mvc;
using Temporalio.Api.Enums.V1;
using Temporalio.Exceptions;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/stretchspaces")]
public class SharedWebspaceController(ITemporalClient temporalClient, ITenantStore tenantStore, IDesiredStateStore<SharedWebspaceData> desiredStateStore) : ControllerBase
{
    /// <summary>
    /// Update Shared Webspace
    /// </summary>
    /// <remarks>
    /// Updates the desired state of a shared webspace. This operation is asynchronous and may take some time to complete. The response will indicate whether the update was accepted, completed successfully, or if there were validation errors.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="systemInstanceId" example="5001234567">The system instance identifier.</param>
    /// <param name="webspace">The shared webspace data to update.</param>
    /// <param name="transactionId" example="waas-update-3fa85f64-5717-4562-b3fc-2c963f66afa6">The transaction identifier. Defaults to a generated value when omitted.</param>
    /// <returns>The updated shared webspace.</returns>
    [HttpPut("{systemInstanceId}")]
    public async Task<IActionResult> UpdateSharedWebspace(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId,
        [FromBody] Space.Classic.ViewModel.SharedWebspace webspace,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    )
    {
        transactionId ??= $"{Guid.NewGuid()}";

        var tenantEntity = await tenantStore.Get(tenant);

        if (tenantEntity is null)
            return NotFound();

        #region Update Desired State

        using var transaction = await desiredStateStore.BeginTransaction();

        await desiredStateStore.Lock(transaction, stackInstanceId, systemInstanceId);

        var desiredState = await desiredStateStore.Read(transaction, tenantEntity.Id, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        desiredState.Data.Webspace.Apply(webspace);

        desiredState = await desiredStateStore.Save(transaction, desiredState);

        var workflowId = $"{transactionId}-webspace-update";

        await desiredStateStore.Schedule(transaction, workflowId, stackInstanceId, systemInstanceId);

        await transaction.CommitAsync();

        #endregion

        #region Dispath Workflow

        using var dispathTransaction = await desiredStateStore.BeginTransaction();

        await desiredStateStore.Dispatched(dispathTransaction, workflowId);

        await temporalClient.StartWorkflowAsync(
            (PublishWorkflow workflow) => workflow.RunAsync(stackInstanceId, systemInstanceId, transactionId, webspace),
            new WorkflowOptions
            {
                Id = workflowId,
                TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                Rpc = new RpcOptions { CancellationToken = HttpContext.RequestAborted },
            });

        await dispathTransaction.CommitAsync();

        #endregion

        #region Await Result

        var result = await temporalClient
            .GetWorkflowHandle(workflowId)
            .GetResultAsync<WaasResult<SharedWebspaceData>>(
                rpcOptions: new RpcOptions { CancellationToken = HttpContext.RequestAborted });

        #endregion

        if (result is null)
            return Accepted(desiredState!.Data.Space.ToViewModel(desiredState.SystemInstanceId!.Value));

        if (result.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = result.ValidationErrors });

        return Ok(result.DesiredState!.Data.Space.ToViewModel(result.DesiredState.SystemInstanceId!.Value));
    }

    /// <summary>
    /// Read Shared Webspace
    /// </summary>
    /// <remarks>
    /// Retrieves the desired state of a shared webspace.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="systemInstanceId" example="5001234567">The system instance identifier.</param>
    /// <returns>The shared webspace data.</returns>
    [HttpGet("{systemInstanceId}")]
    public async Task<IActionResult> ReadSharedWebspace(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId
    )
    {
        var tenantEntity = await tenantStore.Get(tenant);

        if (tenantEntity is null)
            return NotFound();

        var desiredState = await desiredStateStore.Read(tenantEntity.Id, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        var webspace = desiredState.Data.Space.ToViewModel(desiredState.SystemInstanceId!.Value);

        return Ok(webspace);
    }
}