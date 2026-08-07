using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Temporalio.Extensions.Hosting;
using WaaS.Webshield.Workflow;
using WaaS.Common.Workflow;
using WaaS.Webshield.DesiredState;

var builder = WebApplication.CreateBuilder(args);

// Secrets arrive as one file per key from a mounted Kubernetes Secret, so
// "ConnectionStrings__WaaS" as a filename becomes ConnectionStrings:WaaS.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

var waasConnectionString = builder.Configuration.GetConnectionString("WaaS")
    ?? throw new InvalidOperationException("Missing connection string for WaaS");

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Missing connection string for RabbitMq");

builder.Services.AddScoped<IStackInstanceStore>(
    serviceProvider => new StackInstanceStore(waasConnectionString)
);
builder.Services.AddDesiredStateStore<WebshieldData>(waasConnectionString);
builder.Services.AddTenantStore(waasConnectionString);

builder.Services.AddScoped<ISslProxyRepository>(
    serviceProvider => new SslProxyRepository(waasConnectionString)
);

builder.Services.AddSingleton<IRabbitMqPublisher>(
    serviceProvider => new RabbitMqPublisher(
        rabbitMqConnectionString,
        builder.Configuration["RabbitMq:Exchange"]
            ?? throw new InvalidOperationException("Missing RabbitMq:Exchange"))
);

builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        "default",
        WorkflowDefinitions.DefaultTaskQueue)
    .AddScopedActivities<WaasActivities<WebshieldData>>()
    .AddScopedActivities<WebshieldActivities>()
    .AddWorkflow<PublishWebshieldWorkflow>();

builder.Services.AddHealthChecks()
    .AddWaasDatabaseCheck(waasConnectionString);

var app = builder.Build();

// Liveness excludes every tagged check, so it answers as long as the process is
// running. Readiness runs the "ready"-tagged checks, so a database outage takes
// the pod out of rotation without triggering a restart loop.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
