namespace WaaS.Workflow;

using Temporalio.Workflows;

[Workflow]
public class WaitForAckWorkflow
{
    private bool _isSignaled = false;

    [WorkflowSignal]
    public async Task ReceiveCompletionSignalAsync(IDesiredState<SharedWebspaceData> desiredState)
    {
        // Update the state flag to unblock WaitConditionAsync
        _isSignaled = true;
        await Task.CompletedTask;
    }

    [WorkflowRun]
    public async Task RunAsync(ulong stackInstanceId, ulong systemInstanceId, Space.Classic.ViewModel.SharedWebspace webspace)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        await Workflow.WaitConditionAsync(() => _isSignaled);

        await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.SendNotification(stackInstanceId, systemInstanceId),
            options
        );
    }
}