namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
[method: WorkflowInit]
public class PublishWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private readonly HashSet<string> _pending = [];

    [WorkflowRun]
    public async Task RunAsync(ulong stackInstanceId, ulong systemInstanceId)
    {
        await Workflow.WaitConditionAsync(() => _pending.Count == 0 && Workflow.AllHandlersFinished);
    }

    [WorkflowUpdate]
    public async Task<WaasContext<SharedWebspaceData>> PublishDesiredState(string transactionId)
    {
        _pending.Add(transactionId);

        var waasContext = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.ReadWaasContext(transactionId, stackInstanceId, systemInstanceId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );

        waasContext = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.SendToBackend(waasContext),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(15) }
        );

        return waasContext;
    }

    [WorkflowSignal]
    public async Task ReceiveCompletionSignalAsync(string transactionId)
    {
        _pending.Remove(transactionId);

        await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.SendNotification(transactionId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );
    }
}
