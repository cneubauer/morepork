using Dapper;
using Npgsql;
using Temporalio.Api.Enums.V1;

namespace WaaS.WebApi;

public class WorkflowExecutor(ITemporalClient temporalClient, IConfiguration configuration, ILogger<WorkflowExecutor> logger) : BackgroundService
{
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(1);

    private const string ClaimSql = """
        DELETE FROM outbox
        WHERE workflow_id IN (
            SELECT workflow_id FROM outbox
            WHERE leased_until < (NOW() AT TIME ZONE 'utc')
            ORDER BY workflow_id FOR UPDATE SKIP LOCKED LIMIT 10
        )
        RETURNING workflow_id AS WorkflowId, stack_instance_id AS StackInstanceId, system_instance_id AS SystemInstanceId;
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
                entry.WorkflowId,
                entry.StackInstanceId,
                entry.SystemInstanceId);

            var options = new WorkflowOptions
            {
                Id = entry.WorkflowId,
                TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
            };

            await temporalClient.StartWorkflowAsync(
                (PublishWorkflow workflow) => workflow.RunAsync(
                    entry.WorkflowId,
                    (ulong)entry.StackInstanceId,
                    (ulong)entry.SystemInstanceId
                ),
                options
            );
        }

        await transaction.CommitAsync(stoppingToken);
    }

    private sealed record OutboxEntry(string WorkflowId, long StackInstanceId, long SystemInstanceId);
}
