namespace WaaS.Space.Classic.Workflow;

using System.Collections.Concurrent;
using Temporalio.Workflows;

[Workflow]
[method:WorkflowInit]
public class PublishClassicWebspaceWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private bool _closed = false;

    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    private readonly ConcurrentQueue<WaasContext<SharedWebspaceData>> _queue = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Pending => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Acknowledged => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> PublishClassicWebspace(ulong stackInstanceId, ulong systemInstanceId)
    {
        while (!_closed && !_queue.IsEmpty)
        {
            if (!_queue.TryDequeue(out var waasContext))
            {
                await Workflow.DelayAsync(TimeSpan.FromMicroseconds(100));
                continue;
            }

            var webshieldWorkflow = Workflow.ExecuteChildWorkflowAsync(
                (PatchWebshieldMappingsWorkflow workflow) => workflow.PatchWebshieldMappings(waasContext),
                new()
                {
                    Id = $"webshield-{stackInstanceId}-{systemInstanceId}-{waasContext.TransactionId}",
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
                webshieldWorkflow,
                updateProductDns
            );

            await Workflow.ExecuteActivityAsync(
                (ClassicWebspaceActivities act) => act.MarkAsApplied(waasContext),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10)
                }
            );

            _acknowledged.Add(waasContext.TransactionId);
        }

        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);

        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<WaasContext<SharedWebspaceData>> PublishDesiredState(string transactionId)
    {
        _closed = false;

        var waasContext = await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.ReadWaasContext(transactionId, stackInstanceId, systemInstanceId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        waasContext = await Workflow.ExecuteActivityAsync(
            (ClassicWebspaceActivities act) => act.SendToTechMw(waasContext),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(15)
            }
        );

        _pending.Add(transactionId);

        _queue.Enqueue(waasContext);

        return waasContext;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.SendNotification(transactionId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        _acknowledged.Add(transactionId);
        _pending.Remove(transactionId);

        if (_pending.Count == 0)
            _closed = true;
    }
}
