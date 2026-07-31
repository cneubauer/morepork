namespace MyNamespace;

using Temporalio.Workflows;

[Workflow]
public class ReadWorkflow
{
    [WorkflowRun]
    public async Task<DesiredState?> RunAsync(ulong stackInstanceId, ulong systemInstanceId)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        return await Workflow.ExecuteActivityAsync(
            (DesiredStateActivities act) => act.Read(stackInstanceId, systemInstanceId),
            options
        );
    }
}