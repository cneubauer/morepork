using Microsoft.AspNetCore.Mvc;
using Temporalio.Api.Enums.V1;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/webspaces")]
public class ClassicWebspaceController(
    ITemporalClient temporalClient,
    ITenantStore tenantStore,
    IStackInstanceStore stackInstanceStore,
    IDesiredStateStore<SharedWebspaceData> desiredStateStore
) : ControllerBase
{
    /// <summary>
    /// Update Classic Webspace
    /// </summary>
    /// <remarks>
    /// Updates the desired state of a classic webspace. This operation is asynchronous and may take some time to complete. The response will indicate whether the update was accepted, completed successfully, or if there were validation errors.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="systemInstanceId" example="5001234567">The system instance identifier.</param>
    /// <param name="webspace">The classic webspace data to update.</param>
    /// <param name="transactionId" example="waas-update-3fa85f64-5717-4562-b3fc-2c963f66afa6">The transaction identifier. Defaults to a generated value when omitted.</param>
    /// <returns>The updated shared webspace.</returns>
    [HttpPut("{systemInstanceId}")]
    public async Task<IActionResult> UpdateClassicWebspace(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId,
        [FromBody] Space.Classic.ViewModel.SharedWebspace webspace,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    )
    {
        transactionId ??= $"{Guid.NewGuid()}";

        #region Validate

        var tenantEntity = await tenantStore.Get(tenant);

        if (tenantEntity is null)
            return NotFound();

        var stackInstance = await stackInstanceStore.Read(stackInstanceId);

        if (stackInstance is null || stackInstance.TenantId != tenantEntity.Id)
            return NotFound();

        #endregion

        #region Update Desired State

        await using var transaction = await desiredStateStore.BeginTransaction();
        await using var connection = transaction.Connection;

        await desiredStateStore.Lock(transaction, stackInstanceId, systemInstanceId);

        var desiredState = await desiredStateStore.Read(transaction, tenantEntity.Id, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        desiredState.Data.Webspace.Apply(webspace);

        desiredState = await desiredStateStore.Save(transaction, desiredState);

        await desiredStateStore.Schedule(transaction, transactionId, stackInstanceId, systemInstanceId);

        await transaction.CommitAsync();

        #endregion

        #region Dispath Workflow

        await using var dispatchTransaction = await desiredStateStore.BeginTransaction();
        await using var dispatchConnection = dispatchTransaction.Connection;

        await desiredStateStore.Dispatched(dispatchTransaction, transactionId);

        var resourceId = $"webspace-{stackInstanceId}-{systemInstanceId}";

        var startOperation = WithStartWorkflowOperation.Create(
            (PublishClassicWebspaceWorkflow workflow) => workflow.RunAsync(stackInstanceId, systemInstanceId),
            new WorkflowOptions
            {
                Id = resourceId,
                TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            });

        var result = await temporalClient.ExecuteUpdateWithStartWorkflowAsync(
            (PublishClassicWebspaceWorkflow workflow) => workflow.PublishDesiredState(transactionId),
            new WorkflowUpdateWithStartOptions(startOperation)
            {
                Rpc = new RpcOptions { CancellationToken = HttpContext.RequestAborted },
            });

        await dispatchTransaction.CommitAsync();

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
        #region Validate

        var tenantEntity = await tenantStore.Get(tenant);

        if (tenantEntity is null)
            return NotFound();

        var stackInstance = await stackInstanceStore.Read(stackInstanceId);

        if (stackInstance is null || stackInstance.TenantId != tenantEntity.Id)
            return NotFound();

        #endregion

        var desiredState = await desiredStateStore.Read(tenantEntity.Id, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        var webspace = desiredState.Data.Webspace.ToViewModel(desiredState.SystemInstanceId!.Value);

        return Ok(webspace);
    }
}