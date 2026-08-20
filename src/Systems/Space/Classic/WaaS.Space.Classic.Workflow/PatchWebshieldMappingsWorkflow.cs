namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
public class PatchWebshieldMappingsWorkflow
{

    [WorkflowRun]
    public async Task PatchWebshieldMappings(WaasContext<SharedWebspaceData> context)
    {
        
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        
    }
}
