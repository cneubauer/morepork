namespace WaaS.Workflow;

using Temporalio.Workflows;

[Workflow]
public class PublishWorkflow
{
    [WorkflowRun]
    public async Task<WaasResult<SharedWebspaceData>> RunAsync(ulong stackInstanceId, ulong systemInstanceId, Space.Classic.ViewModel.SharedWebspace webspace)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        var desiredState = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.Read(stackInstanceId, systemInstanceId),
            options
        ) ?? throw new Exception($"Desired state not found for stackInstanceId: {stackInstanceId}, systemInstanceId: {systemInstanceId}");

        var childOptions = new ChildWorkflowOptions
        {
            Id = $"wait-notify-{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}",
            ParentClosePolicy = ParentClosePolicy.Abandon,
            // IdReusePolicy = Temporalio.Api.Enums.V1.WorkflowIdReusePolicy.RejectDuplicate ,
        };

        await Workflow.StartChildWorkflowAsync(
            (WaitForAckWorkflow child) => child.RunAsync(stackInstanceId, systemInstanceId),
            childOptions
        );

        return new WaasResult<SharedWebspaceData>
        {
            ValidationErrors = [],
            DesiredState = desiredState
        };
    }
}