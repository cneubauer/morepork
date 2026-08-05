using Dapper;
using Npgsql;
using Temporalio.Exceptions;

namespace WaaS.WebApi;

public class WorkflowExecutor(ITemporalClient temporalClient, IConfiguration configuration, ILogger<WorkflowExecutor> logger) : BackgroundService
{
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                await using var transaction = await connection.BeginTransactionAsync(stoppingToken);

                var entries = await connection.QueryAsync<OutboxEntry>(ClaimSql, transaction: transaction);

                foreach (var entry in entries)
                    await StartWorkflow(entry, stoppingToken);

                await transaction.CommitAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to dispatch outbox entries");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task StartWorkflow(OutboxEntry entry, CancellationToken stoppingToken)
    {
        var options = new WorkflowOptions
        {
            Id = entry.TransactionId,
            TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
        };

        try
        {
            await temporalClient.StartWorkflowAsync(
                (PublishWorkflow workflow) => workflow.RunAsync(
                    (ulong)entry.StackInstanceId,
                    (ulong)entry.SystemInstanceId,
                    new Space.Classic.ViewModel.SharedWebspace()),
                options
            );
        }
        catch (WorkflowAlreadyStartedException)
        {
            logger.LogInformation("Workflow {TransactionId} already started", entry.TransactionId);
        }
    }

    private sealed record OutboxEntry(string TransactionId, long StackInstanceId, long SystemInstanceId);
}
