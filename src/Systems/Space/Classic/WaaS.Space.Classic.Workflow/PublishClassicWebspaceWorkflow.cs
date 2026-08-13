namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
[method: WorkflowInit]
public class PublishClassicWebspaceWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private bool _closed = false;

    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Pending => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Acknowledged => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> StartPublishingClassicWebspace(ulong stackInstanceId, ulong systemInstanceId)
    {
        await Workflow.WaitConditionAsync(() => _closed && Workflow.AllHandlersFinished);
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
            (ClassicWebspaceActivities act) => act.SendToTechMw(waasContext),
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

        if (_pending.Count == 0)
            _closed = true;
    }
}
