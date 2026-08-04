using Dapper;

namespace WaaS.WebApi;

public class WorkflowExecutor(ITemporalClient temporalClient, string connectionString) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for postgress notify
                connection.Notification += async (_sender, eventArgs) =>
                {
                    var payload = eventArgs.Payload;
                    var parts = payload.Split(':');

                    if (parts.Length != 3)
                    {
                        Console.WriteLine($"Invalid notification payload: {payload}");
                        return;
                    }

                    var workflowName = parts[0];
                    var stackInstanceId = ulong.Parse(parts[1]);
                    var systemInstanceId = ulong.Parse(parts[2]);

                    var options = new WorkflowOptions
                    {
                        Id = $"dynamic-workflow-{Guid.NewGuid()}",
                        TaskQueue = WorkflowDefinitions.DefaultTaskQueue,
                    };

                    var workflowHandle = await temporalClient.StartWorkflowAsync(
                        workflowName,
                        [stackInstanceId, systemInstanceId],
                        options
                    );
                };

                await connection.ExecuteAsync("LISTEN workflow_notify", stoppingToken);
            }
            catch (Exception ex)
            {
                // Log the exception and continue the loop
                Console.WriteLine($"Error executing workflow: {ex.Message}");
            }

            // Wait for a short period before checking again
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}