namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
public class PublishWorkflow
{
    [WorkflowRun]
    public async Task<WaasContext<SharedWebspaceData>> RunAsync(string transactionId, ulong stackInstanceId, ulong systemInstanceId)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) };

        var waasContext = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.Read(transactionId, stackInstanceId, systemInstanceId),
            options
        ) ?? throw new Exception($"Desired state not found for stackInstanceId: {stackInstanceId}, systemInstanceId: {systemInstanceId}");

        waasContext = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.Publish(waasContext),
            options
        ) ?? throw new Exception($"Desired state not found for stackInstanceId: {stackInstanceId}, systemInstanceId: {systemInstanceId}");

        var childOptions = new ChildWorkflowOptions
        {
            Id = $"{transactionId}-await-notify",
            ParentClosePolicy = ParentClosePolicy.Abandon,
        };

        await Workflow.StartChildWorkflowAsync(
            (WaitForAckWorkflow child) => child.RunAsync(stackInstanceId, systemInstanceId),
            childOptions
        );

        return waasContext;
    }
}