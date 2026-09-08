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
    /// Updates the desired state of a classic webspace. This operation executes TechMW synchronously and returns the updated desired state, then continues asynchronous reconciliation (Webshield, DNS, ACKs) in the background.
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

        var resourceId = $"webspace-{stackInstanceId}-{systemInstanceId}";

        #region Rate Limit based on workflow queue
        // try
        // {
        //     var pending = await temporalClient
        //         .GetWorkflowHandle<PublishClassicWebspaceWorkflow>(resourceId)
        //         .QueryAsync(wf => wf.PendingTransactions);

        //     if (pending.Count >= MaxQueueDepth)
        //     {
        //         Response.Headers.RetryAfter = "5";
        //         return StatusCode(StatusCodes.Status503ServiceUnavailable,
        //             new { Error = "Too many in-flight transactions for this webspace." });
        //     }
        // }
        // catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        // {
        //     // No run yet — nothing queued.
        // }
        #endregion

        #region Validate

        var tenantEntity = await tenantStore.Get(tenant);

        if (tenantEntity is null)
            return NotFound();

        var stackInstance = await stackInstanceStore.Read(stackInstanceId);

        if (stackInstance is null || stackInstance.TenantId != tenantEntity.Id)
            return NotFound();

        // TODO: Add unique domain validation
        // TODO: Add Desired State consistency validation

        #endregion

        // TODO: Add credential convertion

        #region Update Desired State

        await using var transaction = await desiredStateStore.BeginTransaction();

        await desiredStateStore.Lock(transaction, stackInstanceId, systemInstanceId);

        var desiredState = await desiredStateStore.Read(transaction, tenantEntity.Id, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        desiredState.Data.Webspace.Apply(webspace);

        var saveResult = await desiredStateStore.Save(transaction, desiredState, transactionId);
        desiredState = saveResult.Current;

        var context = new ProcessingContext<SharedWebspaceData>
        {
            Tenant = tenantEntity,
            StackInstance = (StackInstance)stackInstance,
            DesiredState = (DesiredState<SharedWebspaceData>)desiredState,
            TransactionId = transactionId,
            Changes = saveResult.Changes,
        };

        await desiredStateStore.Schedule(transaction, context);

        await transaction.CommitAsync();

        #endregion

        #region Dispath Workflow

        var startOperation = WithStartWorkflowOperation.Create(
            (PublishClassicWebspaceWorkflow workflow) => workflow.PublishClassicWebspace(stackInstanceId, systemInstanceId),
            new WorkflowOptions
            {
                Id = resourceId,
                TaskQueue = "space-classic",
                IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            });

        context = await temporalClient.ExecuteUpdateWithStartWorkflowAsync(
            (PublishClassicWebspaceWorkflow workflow) => workflow.PublishDesiredState(context),
            new WorkflowUpdateWithStartOptions(startOperation)
            {
                Rpc = new() { CancellationToken = HttpContext.RequestAborted },
            }
        );

        await desiredStateStore.Dispatched(transactionId);

        #endregion

        if (context is null)
            return Accepted(desiredState!.Data.Space.ToViewModel(desiredState.SystemInstanceId!.Value));

        if (context.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = context.ValidationErrors });

        Response.Headers.Append("Transaction-Id", transactionId);

        return Accepted(context.DesiredState!.Data.Space.ToViewModel(context.DesiredState.SystemInstanceId!.Value));
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