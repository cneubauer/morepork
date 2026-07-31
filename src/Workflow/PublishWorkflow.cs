namespace MyNamespace;

using Temporalio.Workflows;

[Workflow]
public class PublishWorkflow
{
    [WorkflowRun]
    public async Task<WaasResult> RunAsync(ulong stackInstanceId, Webspace webspace)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        var desiredState = await Workflow.ExecuteActivityAsync(
            (DesiredStateActivities act) => act.Create(stackInstanceId, webspace),
            options
        );

        var result = await Workflow.ExecuteActivityAsync(
            (DesiredStateActivities act) => act.Validate(desiredState),
            options
        );

        if (result.ValidationErrors.Count > 0)
            return result;

        desiredState = await Workflow.ExecuteActivityAsync(
            (DesiredStateActivities act) => act.Populate(desiredState),
            options
        );

        webspace = await Workflow.ExecuteActivityAsync(
            (DesiredStateActivities act) => act.Publish(desiredState),
            options
        );

        var childOptions = new ChildWorkflowOptions
        {
            Id = $"wait-notify-{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}",
            ParentClosePolicy = ParentClosePolicy.Abandon 
        };

        await Workflow.StartChildWorkflowAsync(
            (WaitForAckWorkflow child) => child.RunAsync(webspace),
            childOptions
        );

        return new WaasResult
        {
            ValidationErrors = [],
            DesiredState = desiredState
        };
    }
}