namespace WaaS.Webshield.Workflow;

using Temporalio.Workflows;
using WaaS.Common.Workflow;
using WaaS.Webshield.DesiredState;

[Workflow]
public class PublishWebshieldWorkflow
{
    private readonly HashSet<string> _pendingNodes = [];
    private readonly HashSet<string> _acknowledgedNodes = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> PendingNodes => [.. _pendingNodes];

    [WorkflowQuery]
    public IReadOnlyCollection<string> AcknowledgedNodes => [.. _acknowledgedNodes];

    [WorkflowRun]
    public async Task StartPublishingWebshieldMappings(WaasContext<WebshieldData> context)
    {
        var nodes = await Workflow.ExecuteActivityAsync(
            (WebshieldActivities act) => act.SendToWebshieldNodes(context),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(15),
                RetryPolicy = new() { MaximumAttempts = 3 }
            }
        );

        foreach (var node in nodes)
        {
            _pendingNodes.Add(node);
        }

        if (_pendingNodes.Count > 0)
        {
            await Workflow.WaitConditionAsync(
                () => _pendingNodes.Count == 0,
                TimeSpan.FromSeconds(60)
            );
        }

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<WebshieldData> act) => act.SendNotification(context.TransactionId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId, string node)
    {
        if (_pendingNodes.Remove(node))
        {
            _acknowledgedNodes.Add(node);
        }
        await Task.CompletedTask;
    }
}
