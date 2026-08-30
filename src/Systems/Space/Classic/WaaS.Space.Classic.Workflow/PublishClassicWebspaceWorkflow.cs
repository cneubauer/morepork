namespace WaaS.Space.Classic.Workflow;

using System.Collections.Concurrent;
using ObjectCompare;
using SpaceMiddleware;
using Temporalio.Workflows;
using WaaS.Common.Workflow;
using WaaS.Space.Classic.DesiredState;
using WaaS.Space.DesiredState;
using WaaS.Webshield.Workflow;

[Workflow]
[method:WorkflowInit]
public class PublishClassicWebspaceWorkflow(ulong stackInstanceId, ulong systemInstanceId)
{
    private bool _closed = false;
    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _acknowledged = [];

    private readonly ConcurrentQueue<ProcessingContext<SharedWebspaceData>> _queue = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> InFlightTransactions => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> AcknowledgedTransactions => [.. _acknowledged];

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> PublishClassicWebspace(ulong stackInstanceId, ulong systemInstanceId)
    {
        while (!_closed)
        {
            if (!_queue.TryDequeue(out var context))
            {
                await Workflow.WaitConditionAsync(() => !_queue.IsEmpty || _closed);
                continue;
            }

            Workflow.Logger.LogInformation("Processing transaction {TransactionId} for stack instance {StackInstanceId} and system instance {SystemInstanceId}", context.TransactionId, stackInstanceId, systemInstanceId);
            
            var destination = context.DesiredState.Data.Webspace.Hostname ?? "";

            var mappingsToAdd = context.Changes
                .OfListType<DomainBinding<string>>()
                .Where(x => x.ChangeType == ListChangeType.Added && x.Item is not null)
                .Select(x => new WebshieldMapping(x.Item.DomainName, destination))
                .ToList();

            var mappingsToRemove = context.Changes
                .OfListType<DomainBinding<string>>()
                .Where(x => x.ChangeType == ListChangeType.Removed && x.Item is not null)
                .Select(x => x.Item.DomainName)
                .ToList();

            if (mappingsToAdd.Count > 0 || mappingsToRemove.Count > 0)
            {
                var webshieldContext = await Workflow.ExecuteActivityAsync(
                    (WebshieldActivities act) => act.PatchWebshieldMappings(context, mappingsToAdd, mappingsToRemove),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromSeconds(15),
                        TaskQueue = "webshield"
                    }
                );

                var webshieldWorkflow = Workflow.ExecuteChildWorkflowAsync(
                    (PublishWebshieldWorkflow workflow) => workflow.StartPublishingWebshieldMappings(webshieldContext),
                    new()
                    {
                        Id = $"webshield-{context.TransactionId}",
                        TaskQueue = "webshield",
                    }
                );
            }

            var updateProductDns = Workflow.ExecuteActivityAsync(
                (ClassicWebspaceActivities act) => act.UpdateProductDns(context),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(15)
                }
            );

            await Workflow.WhenAllAsync(
                // webshieldWorkflow,
                updateProductDns
            );

            Workflow.Logger.LogInformation("Product DNS for transaction {TransactionId} and stack instance {StackInstanceId} has been updated", context.TransactionId, stackInstanceId);
        }

        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);

        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<ProcessingContext<SharedWebspaceData>> PublishDesiredState(ProcessingContext<SharedWebspaceData> context)
    {
        _closed = false;

        Workflow.Logger.LogInformation("Got WaaS context: {TransactionId}, Stack ID: {StackInstanceId}", context.TransactionId, stackInstanceId);

        context = await Workflow.ExecuteActivityAsync(
            (ClassicWebspaceActivities act) => act.SendToTechMw(context),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(15)
            }
        );

        Workflow.Logger.LogInformation("Sent WaaS context to Tech MW: {TransactionId}, Stack ID: {StackInstanceId}", context.TransactionId, stackInstanceId);

        _pending.Add(context.TransactionId);

        _queue.Enqueue(context);

        return context;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        await Workflow.ExecuteActivityAsync(
            (ClassicWebspaceActivities act) => act.MarkAsApplied(transactionId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        Workflow.Logger.LogInformation("Marked transaction as applied: {TransactionId}", transactionId);

        await Workflow.ExecuteActivityAsync(
            (WaasActivities<SharedWebspaceData> act) => act.SendNotification(transactionId),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10)
            }
        );

        Workflow.Logger.LogInformation("Sent notification for transaction: {TransactionId}", transactionId);

        _acknowledged.Add(transactionId);
        _pending.Remove(transactionId);

        if (_pending.Count == 0)
            _closed = true;
    }
}
