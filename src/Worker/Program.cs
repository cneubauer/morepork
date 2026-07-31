using MyNamespace;
using Temporalio.Client;
using Temporalio.Worker;

// Create a client to localhost on "default" namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

// Cancellation token to shutdown worker on ctrl+c
using var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

var activities = new DesiredStateActivities();

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions("default")
        .AddActivity(activities.Create)
        .AddActivity(activities.Read)
        .AddActivity(activities.Validate)
        .AddActivity(activities.Populate)
        .AddActivity(activities.Publish)
        .AddActivity(activities.Notify)
        .AddWorkflow<ReadWorkflow>()
        .AddWorkflow<PublishWorkflow>()
        .AddWorkflow<WaitForAckWorkflow>()
);

// Run worker until cancelled
Console.WriteLine("Running worker");
try
{
    await worker.ExecuteAsync(tokenSource.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Worker cancelled");
}