using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using WaaS.Common.Workflow;
using WaaS.Persistence;
using WaaS.Space.Classic.DesiredState;
using WaaS.Space.Classic.Workflow;

namespace WaaS.WebApi;

public class WorkflowExecutor(
    ITemporalClient temporalClient,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WorkflowExecutor> logger
) : BackgroundService
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

        var entries = (await connection.QueryAsync<OutboxEntry>(ClaimSql, transaction: transaction)).ToList();

        if (entries.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var stackInstanceStore = scope.ServiceProvider.GetRequiredService<IStackInstanceStore>();
        var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
        var desiredStateStore = scope.ServiceProvider.GetRequiredService<IDesiredStateStore<SharedWebspaceData>>();

        foreach (var entry in entries)
        {
            logger.LogWarning(
                "Recovering abandoned outbox entry {TransactionId} for stack instance {StackInstanceId}, system instance {SystemInstanceId}",
                entry.TransactionId,
                entry.StackInstanceId,
                entry.SystemInstanceId);

            var stackInstanceId = (ulong)entry.StackInstanceId;
            var systemInstanceId = (ulong)entry.SystemInstanceId;

            var stackInstance = await stackInstanceStore.Read(stackInstanceId);
            if (stackInstance is null)
            {
                logger.LogWarning("Stack instance {StackInstanceId} not found during outbox sweep", stackInstanceId);
                continue;
            }

            var tenant = await tenantStore.Read(stackInstance.TenantId);
            if (tenant is null)
            {
                logger.LogWarning("Tenant {TenantId} not found during outbox sweep", stackInstance.TenantId);
                continue;
            }

            var desiredState = await desiredStateStore.Read(stackInstanceId, systemInstanceId);
            if (desiredState is null)
            {
                logger.LogWarning("Desired state not found for stack {StackInstanceId}, system {SystemInstanceId} during outbox sweep", stackInstanceId, systemInstanceId);
                continue;
            }

            var waasContext = new WaasContext<SharedWebspaceData>
            {
                TransactionId = entry.TransactionId,
                Tenant = tenant,
                StackInstance = (StackInstance)stackInstance,
                DesiredState = (DesiredState<SharedWebspaceData>)desiredState,
            };

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
                    (PublishClassicWebspaceWorkflow workflow) => workflow.PublishDesiredState(waasContext),
                    new WorkflowUpdateWithStartOptions(startOperation)
                    {
                        Rpc = new() { CancellationToken = stoppingToken },
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch recovered workflow update for transaction {TransactionId}", entry.TransactionId);
            }
        }

        await transaction.CommitAsync(stoppingToken);
    }

    private sealed record OutboxEntry(string TransactionId, long StackInstanceId, long SystemInstanceId);
}
