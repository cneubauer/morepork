using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Temporalio.Extensions.Hosting;
using WaaS.Space.Classic.Workflow;
using WaaS.Common.Workflow;
using WebspaceMiddleware;
using WaaS.Space.Workflow;
using WaaS.Webshield.DesiredState;
using WaaS.Webshield.Workflow;

var builder = WebApplication.CreateBuilder(args);

// Secrets arrive as one file per key from a mounted Kubernetes Secret, so
// "ConnectionStrings__WaaS" as a filename becomes ConnectionStrings:WaaS.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

var waasConnectionString = builder.Configuration.GetConnectionString("WaaS")
    ?? throw new InvalidOperationException("Missing connection string for WaaS");

builder.Services
    .AddScoped<IStackInstanceStore>(
        serviceProvider => new StackInstanceStore(waasConnectionString)
    )
    .AddDesiredStateStore<SharedWebspaceData>(waasConnectionString)
    .AddDesiredStateStore<WebshieldData>(waasConnectionString)
    .AddTenantStore(waasConnectionString)
    .AddScoped<IWebshieldMappingService, WebshieldMappingService>()
    .AddHttpClient<ISpaceMiddlewareService<SharedWebspaceData, Webspace>, WebspaceMiddlewareService>(
        client => client.BaseAddress = new Uri(builder.Configuration["WebspaceMiddleware:BaseUrl"]
            ?? throw new InvalidOperationException("Missing WebspaceMiddleware:BaseUrl"))
    );


builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        WorkflowDefinitions.ClientNamespace,
        WorkflowDefinitions.DefaultTaskQueue)
    .AddScopedActivities<WaasActivities<SharedWebspaceData>>()
    .AddScopedActivities<ClassicWebspaceActivities>()
    .AddScopedActivities<WebshieldActivities>()
    .AddWorkflow<PublishClassicWebspaceWorkflow>();

builder.Services.AddHealthChecks()
    .AddWaasDatabaseCheck(waasConnectionString);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
