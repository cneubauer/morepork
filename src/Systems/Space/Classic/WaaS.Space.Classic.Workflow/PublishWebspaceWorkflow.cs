namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
[method: WorkflowInit]
public class PublishWebspaceWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Pending => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Acknowledged => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> RunAsync(ulong stackInstanceId, ulong systemInstanceId)
    {
        await Workflow.WaitConditionAsync(() => _pending.Count == 0 && Workflow.AllHandlersFinished);
        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<WaasContext<SharedWebspaceData>> PublishDesiredState(string transactionId)
    {
        _pending.Add(transactionId);

        var waasContext = await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.ReadWaasContext(transactionId, stackInstanceId, systemInstanceId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );

        waasContext = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.SendToBackend(waasContext),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(15) }
        );

        return waasContext;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        _acknowledged.Add(transactionId);
        _pending.Remove(transactionId);

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.SendNotification(transactionId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );
    }
}
