using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Temporalio.Extensions.Hosting;
using Temporalio.Runtime;
using WaaS.Space.Classic.Workflow;
using WaaS.Common.Workflow;
using WebspaceMiddleware;
using WaaS.Space.Workflow;

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
    .AddScoped<INotificationStore>(
        serviceProvider => new NotificationStore(waasConnectionString)
    )
    .AddDesiredStateStore<SharedWebspaceData>(waasConnectionString)
    .AddTenantStore(waasConnectionString);

builder.Services.AddHttpClient<ISpaceMiddlewareService<SharedWebspaceData, Webspace>, WebspaceMiddlewareService>(
        client => client.BaseAddress = new Uri(builder.Configuration["WebspaceMiddleware:BaseUrl"]
            ?? throw new InvalidOperationException("Missing WebspaceMiddleware:BaseUrl"))
    );

builder.Services.AddHttpClient<PasswordService>(
        client => client.BaseAddress = new Uri(builder.Configuration["PasswordStore:BaseUrl"]
            ?? throw new InvalidOperationException("Missing PasswordStore:BaseUrl"))
    );

// One runtime per process: it owns the Prometheus listener that Prometheus scrapes.
var temporalRuntime = new TemporalRuntime(new()
{
    Telemetry = new() { Metrics = new() { Prometheus = new("0.0.0.0:9000") } },
});

builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        WorkflowDefinitions.ClientNamespace,
        "space-classic")
    .ConfigureOptions(options => options.ClientOptions!.Runtime = temporalRuntime)
    .AddScopedActivities<WaasService<SharedWebspaceData>>()
    .AddScopedActivities<ClassicWebspaceActivities>()
    .AddScopedActivities<PasswordService>()
    .AddWorkflow<PublishClassicWebspaceWorkflow>();

builder.Services.AddHealthChecks()
    .AddWaasDatabaseCheck(waasConnectionString);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
