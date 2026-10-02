using Microsoft.AspNetCore.Mvc;
using Temporalio.Api.Enums.V1;
using WaaS.Common.ViewModel;

namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/stack-instances/{stackInstanceId}/webspaces")]
public class ClassicWebspaceController(
    ITemporalClient temporalClient,
    ITenantStore tenantStore,
    IStackInstanceStore stackInstanceStore,
    IDesiredStateStore<SharedWebspaceData> desiredStateStore,
    PasswordStore passwordService
) : ControllerBase
{

    /// <summary>
    /// Create Classic Webspace
    /// </summary>
    /// <remarks>
    /// Creates a new classic webspace. This operation executes TechMW synchronously and returns the created desired state, then continues asynchronous reconciliation (Webshield, DNS, ACKs) in the background.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="webspace">The classic webspace data to update.</param>
    /// <param name="transactionId" example="waas-update-3fa85f64-5717-4562-b3fc-2c963f66afa6">The transaction identifier. Defaults to a generated value when omitted.</param>
    /// <returns>The updated shared webspace.</returns>
    [HttpPost()]
    public Task<IActionResult> CreateClassicWebspace(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromBody] Space.Classic.ViewModel.SharedWebspace webspace,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    ) => ProvisionClassicWebspace<SharedWebspaceData>(
        tenant,
        stackInstanceId,
        systemInstanceId: null,
        webspace.GetPasswordInfos(),
        desiredState => desiredState.Apply(webspace),
        transactionId ?? $"waas-create-{Guid.NewGuid()}"
    );

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
        var waasContext = await CreateWaasContext(tenant, stackInstanceId, transactionId: "");

        if (waasContext is null)
            return NotFound();

        var desiredState = await desiredStateStore.Read(waasContext.Tenant.Id, stackInstanceId, systemInstanceId);

        if (desiredState is null)
            return NotFound();

        var webspace = desiredState.Data.Webspace.ToViewModel(desiredState.SystemInstanceId);

        return Ok(webspace);
    }

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
    public Task<IActionResult> UpdateClassicWebspace(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId,
        [FromBody] Space.Classic.ViewModel.SharedWebspace webspace,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    ) => ProvisionClassicWebspace<SharedWebspaceData>(
        tenant,
        stackInstanceId,
        systemInstanceId,
        webspace.GetPasswordInfos(),
        desiredState => desiredState.Apply(webspace),
        transactionId ?? $"waas-update-{Guid.NewGuid()}"
    );

    /// <summary>
    /// Delete a Shared Webspace
    /// </summary>
    /// <remarks>
    /// Deletes the desired state of a shared webspace.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="stackInstanceId" example="1234567">The stack instance identifier.</param>
    /// <param name="systemInstanceId" example="5001234567">The system instance identifier.</param>
    /// <param name="transactionId" example="waas-delete-3fa85f64-5717-4562-b3fc-2c963f66afa6">The transaction identifier. Defaults to a generated value when omitted.</param>
    /// <returns>The shared webspace data.</returns>
    [HttpDelete("{systemInstanceId}")]
    public Task<IActionResult> DeleteSharedWebspace(
        [FromRoute] string tenant,
        [FromRoute] ulong stackInstanceId,
        [FromRoute] ulong systemInstanceId,
        [FromHeader(Name = "Transaction-Id")] string? transactionId
    ) => ProvisionClassicWebspace<SharedWebspaceData>(
        tenant,
        stackInstanceId,
        systemInstanceId,
        [],
        desiredState => desiredState.Tombstone(),
        transactionId ?? $"waas-delete-{Guid.NewGuid()}"
    );

    private async Task<IActionResult> ProvisionClassicWebspace<TDesiredState>(
        string tenant,
        ulong stackInstanceId,
        ulong? systemInstanceId,
        IEnumerable<PasswordInfo> passwordInfos,
        Action<IDesiredState<SharedWebspaceData>> modify,
        string transactionId
    ) where TDesiredState : IDesiredStateData, new()
    {
        var waasContext = await CreateWaasContext(tenant, stackInstanceId, transactionId);

        if (waasContext is null)
            return NotFound();

        // TODO: Add unique domain validation
        // TODO: Add Desired State consistency validation

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

        var newTokens = await passwordService.ConvertCredentials(waasContext.Tenant.Name, passwordInfos);

        var context = await desiredStateStore.Upsert(waasContext, systemInstanceId, modify);

        if (context is null)
            return NotFound();

        await passwordService.CommitPasswordTokens(
            context.Tenant.Name,
            context.StackInstance.Id,
            context.DesiredState.SystemInstanceId,
            newTokens
        );

        #region Dispatch Workflow

        var resourceId = $"webspace-{context.StackInstance.Id}-{context.DesiredState.SystemInstanceId}";

        var startOperation = WithStartWorkflowOperation.Create(
            (PublishClassicWebspaceWorkflow workflow) => workflow.PublishClassicWebspace(
                context.StackInstance.Id, 
                context.DesiredState.SystemInstanceId
            ),
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

        await desiredStateStore.RemoveOutboxMessage(context.TransactionId);

        #endregion

        if (context.ValidationErrors.Count > 0)
            return BadRequest(new { Errors = context.ValidationErrors });

        Response.Headers.Append("Transaction-Id", context.TransactionId);

        return Accepted(context.DesiredState!.Data.Space.ToViewModel(context.DesiredState.SystemInstanceId));
    }

    private async Task<WaasContext?> CreateWaasContext(string tenant, ulong stackInstanceId, string transactionId)
    {
        var tenantEntity = await tenantStore.Get(tenant);

        if (tenantEntity is null)
            return null;

        var stackInstance = await stackInstanceStore.Read(stackInstanceId);

        if (stackInstance is null || stackInstance.TenantId != tenantEntity.Id)
            return null;

        return new WaasContext()
        {
            Tenant = tenantEntity,
            StackInstance = (StackInstance)stackInstance,
            TransactionId = transactionId,
        };
    }
}