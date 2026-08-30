namespace WaaS.Space.Classic.Workflow;

using System.Collections.Concurrent;
using Temporalio.Workflows;
using WaaS.Common.Workflow;
using WaaS.Space.Classic.DesiredState;
using WaaS.Webshield.Workflow;

[Workflow]
[method:WorkflowInit]
public class PublishClassicWebspaceWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private bool _closed = false;
    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    private readonly ConcurrentQueue<WaasContext<SharedWebspaceData>> _queue = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> InFlightTransactions => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> AcknowledgedTransactions => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> PublishClassicWebspace(ulong stackInstanceId, ulong systemInstanceId)
    {
        while (!_closed)
        {
            if (!_queue.TryDequeue(out var waasContext))
            {
                await Workflow.WaitConditionAsync(() => !_queue.IsEmpty || _closed);
                continue;
            }

            Workflow.Logger.LogInformation("Processing transaction {TransactionId} for stack instance {StackInstanceId} and system instance {SystemInstanceId}", waasContext.TransactionId, stackInstanceId, systemInstanceId);
            
            // TODO: Determine webshield mappings patch
            var mappingsToAdd = new List<WebshieldMapping>();
            var mappingsToRemove = new List<WebshieldMapping>();

            var webshieldContext = await Workflow.ExecuteActivityAsync(
                (WebshieldActivities act) => act.PatchWebshieldMappings(waasContext, mappingsToAdd, mappingsToRemove),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(15),
                    TaskQueue = "webshield"
                }
            );

            var webshieldWorkflow = Workflow.ExecuteChildWorkflowAsync(
                (PublishWebshieldWorkflow workflow) => workflow.StartPublishingWebshieldMappings(webshieldContext),
                new()
                {
                    Id = $"webshield-{stackInstanceId}-{systemInstanceId}-{waasContext.TransactionId}",
                    TaskQueue = "webshield",
                }
            );

            var updateProductDns = Workflow.ExecuteActivityAsync(
                (ClassicWebspaceActivities act) => act.UpdateProductDns(waasContext),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(15)
                }
            );

            await Workflow.WhenAllAsync(
                // webshieldWorkflow,
                updateProductDns
            );

            Workflow.Logger.LogInformation("Product DNS for transaction {TransactionId} and stack instance {StackInstanceId} has been updated", waasContext.TransactionId, stackInstanceId);
        }

        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);

        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<WaasContext<SharedWebspaceData>> PublishDesiredState(WaasContext<SharedWebspaceData> waasContext)
    {
        _closed = false;

        waasContext = await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.ReadWaasContext(waasContext.TransactionId, stackInstanceId, systemInstanceId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        Workflow.Logger.LogInformation("Got WaaS context: {TransactionId}, Stack ID: {StackInstanceId}", waasContext.TransactionId, stackInstanceId);

        waasContext = await Workflow.ExecuteActivityAsync(
            (ClassicWebspaceActivities act) => act.SendToTechMw(waasContext),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(15)
            }
        );

        Workflow.Logger.LogInformation("Sent WaaS context to Tech MW: {TransactionId}, Stack ID: {StackInstanceId}", waasContext.TransactionId, stackInstanceId);

        _pending.Add(waasContext.TransactionId);

        _queue.Enqueue(waasContext);

        return waasContext;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        await Workflow.ExecuteActivityAsync(
            (ClassicWebspaceActivities act) => act.MarkAsApplied(transactionId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        Workflow.Logger.LogInformation("Marked transaction as applied: {TransactionId}", transactionId);

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.SendNotification(transactionId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        Workflow.Logger.LogInformation("Sent notification for transaction: {TransactionId}", transactionId);

        _acknowledged.Add(transactionId);
        _pending.Remove(transactionId);

        if (_pending.Count == 0)
            _closed = true;
    }
}
