namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;

[Workflow]
public class UpdateProductDnsWorkflow
{

    [WorkflowUpdate]
    public async Task UpdateProductDns(WaasContext<SharedWebspaceData> context)
    {
        
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        
    }
}
