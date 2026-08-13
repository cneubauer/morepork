namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
public class ClassicWebspaceProvisioningWorkflow
{

    [WorkflowUpdate]
    public async Task StartProvisioningClassicWebspace(WaasContext<SharedWebspaceData> context)
    {
        await Workflow.ExecuteChildWorkflowAsync(
            (PublishClassicWebspaceWorkflow wf) => wf.StartPublishingClassicWebspace(context),
            new ChildWorkflowOptions 
            { 
                Id = "independent-123",
                // This detaches the lifecycle. If the parent dies, the child keeps running.
                ParentClosePolicy = ParentClosePolicy.Abandon 
            }
        );
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        
    }
}
