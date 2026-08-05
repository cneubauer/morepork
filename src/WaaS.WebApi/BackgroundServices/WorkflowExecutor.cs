using Dapper;
using Npgsql;
using Temporalio.Api.Enums.V1;

namespace WaaS.WebApi;

/// <summary>
/// The only component that starts workflows. Claims outbox entries and starts the workflow for
/// each; the waiting request finds it by transaction ID.
/// </summary>
public class WorkflowExecutor(ITemporalClient temporalClient, IConfiguration configuration, ILogger<WorkflowExecutor> logger) : BackgroundService
{
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(1);

    private const string ClaimSql = """
        DELETE FROM outbox
        WHERE transaction_id IN (
            SELECT transaction_id FROM outbox ORDER BY transaction_id FOR UPDATE SKIP LOCKED LIMIT 10
        )
        RETURNING transaction_id AS TransactionId, stack_instance_id AS StackInstanceId, system_instance_id AS SystemInstanceId;
        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("DesiredState")!;

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
            var options = new WorkflowOptions
            {
                Id = entry.TransactionId,
                TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
            };

            await temporalClient.StartWorkflowAsync(
                (PublishWorkflow workflow) => workflow.RunAsync(
                    (ulong)entry.StackInstanceId,
                    (ulong)entry.SystemInstanceId,
                    new Space.Classic.ViewModel.SharedWebspace()),
                options
            );
        }

        await transaction.CommitAsync(stoppingToken);
    }

    private sealed record OutboxEntry(string TransactionId, long StackInstanceId, long SystemInstanceId);
}
