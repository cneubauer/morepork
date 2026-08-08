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

builder.Services
    .AddScoped<IStackInstanceStore>(
        serviceProvider => new StackInstanceStore(waasConnectionString)
    )
    .AddDesiredStateStore<WebshieldData>(waasConnectionString)
    .AddTenantStore(waasConnectionString)
    .AddScoped<ISslProxyRepository>(
        serviceProvider => new SslProxyRepository(waasConnectionString)
    )
    .AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        WorkflowDefinitions.ClientNamespace,
        WorkflowDefinitions.DefaultTaskQueue)
    .AddScopedActivities<WaasActivities<WebshieldData>>()
    .AddScopedActivities<WebshieldActivities>()
    .AddWorkflow<PublishWebshieldWorkflow>();

builder.Services.AddHealthChecks()
    .AddWaasDatabaseCheck(waasConnectionString);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
