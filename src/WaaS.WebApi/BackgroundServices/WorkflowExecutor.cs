using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using Temporalio.Api.Enums.V1;

namespace WaaS.WebApi;

public class WorkflowExecutor(
    ITemporalClient temporalClient,
    IConfiguration configuration,
    ILogger<WorkflowExecutor> logger
) : BackgroundService
{
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        }
    };

    private const string ClaimSql = """
        DELETE FROM outbox
        WHERE ctid IN (
            SELECT ctid FROM outbox
            WHERE leased_until < (NOW() AT TIME ZONE 'utc')
            ORDER BY created FOR UPDATE SKIP LOCKED LIMIT 10
        )
        RETURNING context;
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

        var entries = (await connection.QueryAsync<string>(ClaimSql, transaction: transaction)).ToList();

        if (entries.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        foreach (var rawContext in entries)
        {
            ProcessingContext<SharedWebspaceData>? context;
            try
            {
                context = JsonSerializer.Deserialize<ProcessingContext<SharedWebspaceData>>(rawContext, _jsonOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deserialize outbox context: {RawContext}", rawContext);
                continue;
            }

            if (context is null)
            {
                logger.LogWarning("Outbox entry contained null context");
                continue;
            }

            var stackInstanceId = context.StackInstance.Id;
            var systemInstanceId = context.DesiredState.SystemInstanceId;

            logger.LogWarning(
                "Recovering abandoned outbox entry {TransactionId} for stack instance {StackInstanceId}, system instance {SystemInstanceId}",
                context.TransactionId,
                stackInstanceId,
                systemInstanceId);

            var resourceId = $"webspace-{stackInstanceId}-{systemInstanceId}";

            var startOperation = WithStartWorkflowOperation.Create(
                (PublishClassicWebspaceWorkflow workflow) => workflow.PublishClassicWebspace(stackInstanceId, systemInstanceId),
                new WorkflowOptions
                {
                    Id = resourceId,
                    TaskQueue = "space-classic",
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                });

            try
            {
                await temporalClient.ExecuteUpdateWithStartWorkflowAsync(
                    (PublishClassicWebspaceWorkflow workflow) => workflow.PublishDesiredState(context),
                    new WorkflowUpdateWithStartOptions(startOperation)
                    {
                        Rpc = new() { CancellationToken = stoppingToken },
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch recovered workflow update for transaction {TransactionId}", context.TransactionId);
            }
        }

        await transaction.CommitAsync(stoppingToken);
    }
}
