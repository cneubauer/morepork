namespace WaaS.Space.Classic.Workflow;

using System.Collections.Concurrent;
using ObjectCompare;
using Temporalio.Exceptions;
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
    private readonly SortedSet<string> _pending = [];
    private readonly SortedSet<string> _acknowledged = [];

    private readonly ConcurrentQueue<ProcessingContext<SharedWebspaceData>> _queue = [];

    [WorkflowQuery]
    public IReadOnlyCollection<string> PendingTransactions => [.. _pending];

    [WorkflowQuery]
    public IReadOnlyCollection<string> AcknowledgedTransactions => [.. _acknowledged];


    [WorkflowUpdateValidator(nameof(PublishDesiredState))]
    public void ValidatePublishDesiredState(ProcessingContext<SharedWebspaceData> context)
    {
        if (_queue.Count >= 5)
            throw new ApplicationFailureException(
                $"Too many update for resource. Try again later",
                errorType: "QueueFull",
                nonRetryable: true);

        if (Workflow.CurrentHistoryLength > 40_000)
            throw new ApplicationFailureException(
                "Workflow history limit approaching. Retry later.",
                errorType: "HistoryLimit",
                nonRetryable: true);
    }

    [WorkflowRun]
    public async Task<IReadOnlyCollection<string>> PublishClassicWebspace(ulong stackInstanceId, ulong systemInstanceId)
    {
        Workflow.UpsertTypedSearchAttributes(
            SearchAttributes.StackInstanceId.ValueSet((long)stackInstanceId),
            SearchAttributes.SystemInstanceId.ValueSet((long)systemInstanceId),
            SearchAttributes.StateNamespace.ValueSet("ClassicWebspace")
        );

        while (!_closed)
        {
            if (!_queue.TryDequeue(out var context))
            {
                await Workflow.WaitConditionAsync(() => !_queue.IsEmpty || _closed);
                continue;
            }

            Workflow.Logger.LogInformation("Processing transaction {TransactionId} for stack instance {StackInstanceId} and system instance {SystemInstanceId}", context.TransactionId, stackInstanceId, systemInstanceId);
            
            var destination = context.DesiredState.Data.Webspace.Hostname
                ?? throw new ApplicationFailureException("Hostname is required", errorType: "InvalidState", nonRetryable: true);

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

            var tasks = new List<Task>();

            if (mappingsToAdd.Count > 0 || mappingsToRemove.Count > 0)
            {
                var webshieldContext = await Workflow.ExecuteActivityAsync(
                    (WebshieldActivities act) => act.PatchWebshieldMappings(context, mappingsToAdd, mappingsToRemove),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromSeconds(15),
                        TaskQueue = PublishWebshieldWorkflow.DefaultTaskQueue
                    }
                );

                var webshieldWorkflow = Workflow.ExecuteChildWorkflowAsync(
                    (PublishWebshieldWorkflow workflow) => workflow.StartPublishingWebshieldMappings(webshieldContext),
                    new()
                    {
                        Id = $"webshield-{context.TransactionId}",
                        TaskQueue = PublishWebshieldWorkflow.DefaultTaskQueue,
                    }
                );

                tasks.Add(webshieldWorkflow);
            }

            var updateProductDns = Workflow.ExecuteLocalActivityAsync(
                (ClassicWebspaceActivities act) => act.UpdateProductDns(context),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(15),
                    Summary = "Updating product DNS",
                }
            );

            tasks.Add(updateProductDns);

            // Wait for child workflows to finish
            await Workflow.WhenAllAsync(tasks);

            Workflow.Logger.LogInformation("Product DNS for transaction {TransactionId} and stack instance {StackInstanceId} has been updated", context.TransactionId, stackInstanceId);

            // Wait for TechMW notification to arrive
            await Workflow.WaitConditionAsync(() => _acknowledged.Contains(context.TransactionId));

            await Workflow.ExecuteLocalActivityAsync(
                (WaasActivities<SharedWebspaceData> act) => act.SendFinalAckNotification(context.TransactionId),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    Summary = "Sending final ACK notification",
                }
            );
        }

        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);

        return [.. _acknowledged];
    }

    [WorkflowUpdate]
    public async Task<ProcessingContext<SharedWebspaceData>> PublishDesiredState(ProcessingContext<SharedWebspaceData> context)
    {
        _closed = false;

        Workflow.UpsertTypedSearchAttributes(
            SearchAttributes.Tenant.ValueSet(context.Tenant.Name)
        );

        Workflow.Logger.LogInformation(
            "Publishing new Desired State version for Stack Instance {StackInstanceId} System Instance {SystemInstanceId} [{TransactionId}]",
            stackInstanceId,
            systemInstanceId,
            context.TransactionId
        );

        context = await Workflow.ExecuteLocalActivityAsync(
            (ClassicWebspaceActivities act) => act.SendToTechMw(context),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(15),
                Summary = "Sending Desired State to TechMW",
            }
        );

        _pending.Add(context.TransactionId);

        _queue.Enqueue(context);

        return context;
    }

    [WorkflowSignal]
    public async Task ReceiveBackendNotification(string transactionId)
    {
        if (!_pending.Contains(transactionId)) return;

        var includedTransactions = _pending
            .TakeWhile(x => x != transactionId)
            .Append(transactionId)
            .ToList();

        foreach (var transaction in includedTransactions)
        {
            await Workflow.ExecuteLocalActivityAsync(
                (ClassicWebspaceActivities act) => act.MarkAsApplied(transaction),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    Summary = "Marking Desired State as applied",
                }
            );

            await Workflow.ExecuteLocalActivityAsync(
                (WaasActivities<SharedWebspaceData> act) => act.SendIntermediateNotification(transaction),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    Summary = "Sending intermediate notification",
                }
            );

            _acknowledged.Add(transaction);
            _pending.Remove(transaction);
        }

        if (_pending.Count == 0)
            _closed = true;
    }
}
