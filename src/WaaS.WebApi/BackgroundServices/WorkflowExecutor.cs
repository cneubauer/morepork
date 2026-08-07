using Dapper;
using Npgsql;
using Temporalio.Api.Enums.V1;

namespace WaaS.WebApi;

public class WorkflowExecutor(ITemporalClient temporalClient, IConfiguration configuration, ILogger<WorkflowExecutor> logger) : BackgroundService
{
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(1);

    private const string ClaimSql = """
        DELETE FROM outbox
        WHERE transaction_id IN (
            SELECT transaction_id FROM outbox
            WHERE leased_until < (NOW() AT TIME ZONE 'utc')
            ORDER BY transaction_id FOR UPDATE SKIP LOCKED LIMIT 10
        )
        RETURNING transaction_id AS TransactionId, stack_instance_id AS StackInstanceId, system_instance_id AS SystemInstanceId;
        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("WaaS")!;

        using var timer = new PeriodicTimer(_sweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await Dispatch(connectionString, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to dispatch outbox entries");
            }
        }
    }

    private async Task Dispatch(string connectionString, CancellationToken stoppingToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(stoppingToken);
        await using var transaction = await connection.BeginTransactionAsync(stoppingToken);

        var entries = await connection.QueryAsync<OutboxEntry>(ClaimSql, transaction: transaction);

        foreach (var entry in entries)
        {
            logger.LogWarning(
                "Recovering abandoned outbox entry {TransactionId} for stack instance {StackInstanceId}, system instance {SystemInstanceId}",
                entry.TransactionId,
                entry.StackInstanceId,
                entry.SystemInstanceId);

            var stackInstanceId = (ulong)entry.StackInstanceId;
            var systemInstanceId = (ulong)entry.SystemInstanceId;

            var resourceId = $"webspace-{stackInstanceId}-{systemInstanceId}";

            // Must be update-with-start, not a plain start: a reconciler created with an empty
            // pending set would publish nothing and idle out.
            var startOperation = WithStartWorkflowOperation.Create(
                (PublishWebspaceWorkflow workflow) => workflow.RunAsync(stackInstanceId, systemInstanceId),
                new WorkflowOptions
                {
                    Id = resourceId,
                    TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                });

            await temporalClient.ExecuteUpdateWithStartWorkflowAsync(
                (PublishWebspaceWorkflow workflow) => workflow.PublishDesiredState(entry.TransactionId),
                new WorkflowUpdateWithStartOptions(startOperation)
            );
        }

        await transaction.CommitAsync(stoppingToken);
    }

    private sealed record OutboxEntry(string TransactionId, long StackInstanceId, long SystemInstanceId);
}
