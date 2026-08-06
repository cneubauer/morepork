namespace WaaS.Workflow;

using Temporalio.Workflows;

[Workflow]
public class WaitForAckWorkflow
{
    private bool _isCompleted;

    [WorkflowSignal]
    public async Task ReceiveCompletionSignalAsync()
    {
        _isCompleted = true;
        await Task.CompletedTask;
    }

    [WorkflowRun]
    public async Task RunAsync(ulong stackInstanceId, ulong systemInstanceId)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        await Workflow.WaitConditionAsync(() => _isCompleted);

        await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.SendNotification(stackInstanceId, systemInstanceId),
            options
        );
    }
}