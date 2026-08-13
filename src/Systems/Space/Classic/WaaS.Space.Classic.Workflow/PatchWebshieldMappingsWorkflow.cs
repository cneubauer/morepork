namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
public class PatchWebshieldMappingsWorkflow
{

    [WorkflowUpdate]
    public async Task StartPatchingWebshield(WaasContext<SharedWebspaceData> context)
    {
        
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        
    }
}
