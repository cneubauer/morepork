namespace WaaS.Webshield.Workflow;

using Temporalio.Workflows;
using WaaS.Common.Workflow;
using WaaS.Webshield.DesiredState;

[Workflow]
[method: WorkflowInit]
public class PublishWebshieldWorkflow(ulong stackInstanceId)
{
    private readonly Dictionary<string, HashSet<string>> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    [WorkflowQuery]
    public IReadOnlyDictionary<string, HashSet<string>> Pending => _pending;

    [WorkflowQuery]
    public IReadOnlyCollection<string> Acknowledged => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> RunAsync(ulong stackInstanceId)
    {
        await Workflow.WaitConditionAsync(() => _pending.Count == 0 && Workflow.AllHandlersFinished);
        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<WaasContext<WebshieldData>> PublishDesiredState(string transactionId)
    {
        var waasContext = await Workflow.ExecuteActivityAsync(
            (WaasActivities<WebshieldData> act) => act.ReadWaasContext(transactionId, stackInstanceId, 0),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );

        var nodes = await Workflow.ExecuteActivityAsync(
            (WebshieldActivities act) => act.SendToBackend(waasContext),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(15) }
        );

        _pending.Add(transactionId, [.. nodes]);

        return waasContext;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId, string node)
    {
        var pendingNodes = _pending.GetValueOrDefault(transactionId);

        if (pendingNodes is null)
            return;

        pendingNodes.Remove(node);

        if (pendingNodes.Count > 0)
            return;

        _acknowledged.Add(transactionId);
        _pending.Remove(transactionId);

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<WebshieldData> act) => act.SendNotification(transactionId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );
    }
}
