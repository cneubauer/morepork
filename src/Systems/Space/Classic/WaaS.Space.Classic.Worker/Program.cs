using Temporalio.Extensions.Hosting;
using WaaS.Persistence;
using WaaS.Space.Classic.DesiredState;
using WaaS.Workflow;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DesiredState")!;

builder.Services.AddDesiredStateStore<SharedWebspaceData>(connectionString);

builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        "default",
        WorkflowDefinitions.DefaultTaskQueue)
    .AddScopedActivities<SharedWebspaceActivities>()
    .AddWorkflow<PublishWorkflow>()
    .AddWorkflow<WaitForAckWorkflow>();

await builder.Build().RunAsync();
