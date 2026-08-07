using Temporalio.Extensions.Hosting;
using WaaS.Space.Classic.Workflow;
using WaaS.Common.Workflow;
using WebspaceMiddleware;
using WaaS.Space.Workflow;

var builder = Host.CreateApplicationBuilder(args);

var waasConnectionString = builder.Configuration.GetConnectionString("WaaS")
    ?? throw new InvalidOperationException("Missing connection string for WaaS");

builder.Services.AddScoped<IStackInstanceStore>(
    serviceProvider => new StackInstanceStore(waasConnectionString)
);
builder.Services.AddDesiredStateStore<SharedWebspaceData>(waasConnectionString);
builder.Services.AddTenantStore(waasConnectionString);

builder.Services.AddHttpClient<ISpaceMiddlewareService<SharedWebspaceData, Webspace>, WebspaceMiddlewareService>(
    client => client.BaseAddress =
        new Uri(builder.Configuration["WebspaceMiddleware:BaseUrl"]!));


builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        "default",
        WorkflowDefinitions.DefaultTaskQueue)
    .AddScopedActivities<SharedWebspaceActivities>()
    .AddWorkflow<PublishWebspaceWorkflow>();

await builder.Build().RunAsync();
