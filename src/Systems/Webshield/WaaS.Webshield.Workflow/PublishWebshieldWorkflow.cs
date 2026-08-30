namespace WaaS.Webshield.Workflow;

using Microsoft.Extensions.Logging;
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
    public async Task<IReadOnlyCollection<string>> StartPublishingWebshieldMappings(WaasContext<WebshieldData> context)
    {
        var nodes = await Workflow.ExecuteActivityAsync(
            (WebshieldActivities act) => act.SendToWebshieldNodes(context),
            WorkflowActivityDefaults.Default
        );

        foreach (var node in nodes)
            _pendingNodes.Add(node);

        var allAcked = await Workflow.WaitConditionAsync(
            () => _pendingNodes.Count == 0,
            Timeout.InfiniteTimeSpan
        );

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<WebshieldData> act) => act.SendNotification(context.TransactionId),
            WorkflowActivityDefaults.Quick
        );

        return [.. _acknowledgedNodes];
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId, string node)
    {
        if (_pendingNodes.Remove(node))
            _acknowledgedNodes.Add(node);

        await Task.CompletedTask;
    }
}
