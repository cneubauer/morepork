namespace WaaS.Workflow;

using Temporalio.Workflows;

[Workflow]
public class PublishWorkflow
{
    [WorkflowRun]
    public async Task<WaasResult<SharedWebspace>> RunAsync(ulong stackInstanceId, ulong systemInstanceId, Space.Classic.ViewModel.SharedWebspace webspace)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        var desiredState = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.Read(stackInstanceId, systemInstanceId),
            options
        );

        // - Validate request data (ViewModel) integrity
        // - Get Desired State (document) from database
        // - Validate ViewModel against Desired State
        // - Apply ViewModel to Desired State
        // - Save Desired State to database
        // - Send Desired State to backend [A]
        // - Send Webshield update based on resulting backend data
        //   - Wait for ACK notification
        // - Send DNS update
        //   - Wait for ACK notification
        // - Wait for ACK notification from backend [A]
        // - Send final notification to client

        // var childOptions = new ChildWorkflowOptions
        // {
        //     Id = $"wait-notify-{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}",
        //     ParentClosePolicy = ParentClosePolicy.Abandon 
        // };

        // await Workflow.StartChildWorkflowAsync(
        //     (WaitForAckWorkflow child) => child.RunAsync(webspace),
        //     childOptions
        // );

        return new WaasResult<SharedWebspace>
        {
            ValidationErrors = [],
            DesiredState = desiredState
        };
    }
}