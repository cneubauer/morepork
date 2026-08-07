using Temporalio.Extensions.Hosting;
using WaaS.Space.Classic.Workflow;
using WaaS.Common.Workflow;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Morepork")!;

builder.Services.AddDesiredStateStore<SharedWebspaceData>(connectionString);

builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        "default",
        WorkflowDefinitions.DefaultTaskQueue)
    .AddScopedActivities<SharedWebspaceActivities>()
    .AddWorkflow<PublishWorkflow>();

await builder.Build().RunAsync();
