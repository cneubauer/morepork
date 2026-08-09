namespace WaaS.Space.Classic.Workflow;

using Temporalio.Workflows;
using WaaS.Webshield.Workflow;

[Workflow]
[method: WorkflowInit]
public class PublishWebspaceWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Pending => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> Acknowledged => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> RunAsync(ulong stackInstanceId, ulong systemInstanceId)
    {
        await Workflow.WaitConditionAsync(() => _pending.Count == 0 && Workflow.AllHandlersFinished);
        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<WaasContext<SharedWebspaceData>> PublishDesiredState(string transactionId)
    {
        _pending.Add(transactionId);

        var waasContext = await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.ReadWaasContext(transactionId, stackInstanceId, systemInstanceId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );

        waasContext = await Workflow.ExecuteActivityAsync(
            (SharedWebspaceActivities act) => act.SendToBackend(waasContext),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(15) }
        );

        var hostname = waasContext.DesiredState.Data.Webspace.Hostname ?? string.Empty;

        var mappings = (waasContext.DesiredState.Data.Webspace.Domains ?? [])
            .Concat(waasContext.DesiredState.Data.Webspace.HttpAccessDomains ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.DomainName))
            .Select(d => new WebshieldMapping(d.DomainName, hostname, d.IsEnabled ?? true))
            .ToList();

        await Workflow.ExecuteActivityAsync(
            (WebshieldActivities act) => act.SyncWebshieldMappings(
                waasContext.StackInstance,
                mappings
            ),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );

        await Workflow.ExecuteChildWorkflowAsync(
            (PublishWebshieldWorkflow wf) => wf.PublishDesiredState(transactionId),
            new ChildWorkflowOptions
            {
                Id = $"webshield-{stackInstanceId}",
            }
        );

        return waasContext;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        _acknowledged.Add(transactionId);
        _pending.Remove(transactionId);

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.SendNotification(transactionId),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) }
        );
    }
}
