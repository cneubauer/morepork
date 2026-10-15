using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Temporalio.Extensions.Hosting;
using Temporalio.Runtime;
using WaaS.Webshield.Workflow;
using WaaS.Common.Workflow;
using WaaS.Webshield.DesiredState;

var builder = WebApplication.CreateBuilder(args);

// Secrets arrive as one file per key from a mounted Kubernetes Secret, so
// "ConnectionStrings__WaaS" as a filename becomes ConnectionStrings:WaaS.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

var waasConnectionString = builder.Configuration.GetConnectionString("WaaS")
    ?? throw new InvalidOperationException("Missing connection string for WaaS");

builder.Services
    .Configure<RabbitMqOptions>(builder.Configuration
    .GetSection("RabbitMq"));

// One runtime per process: it owns the Prometheus listener that Prometheus scrapes,
// so the injected client and the hosted worker's client must share it.
var temporalRuntime = new TemporalRuntime(new()
{
    Telemetry = new() { Metrics = new() { Prometheus = new("0.0.0.0:9000") } },
});

builder.Services
    .AddScoped<IStackInstanceStore>(
        serviceProvider => new StackInstanceStore(waasConnectionString)
    )
    .AddScoped<INotificationStore>(
        serviceProvider => new NotificationStore(waasConnectionString)
    )
    .AddDesiredStateStore<WebshieldData>(waasConnectionString)
    .AddTenantStore(waasConnectionString)
    .AddScoped<ISslProxyRepository>(
        serviceProvider => new SslProxyRepository(waasConnectionString)
    )
    .AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>()
    .AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>()
    .AddHostedService<WebshieldActualStateListener>()
    .AddTemporalClient(options =>
    {
        options.TargetHost = builder.Configuration["Temporal:TargetHost"];
        options.Runtime = temporalRuntime;
    });

builder.Services.AddHttpClient<IPasswordService, PasswordService>(
        client => client.BaseAddress = new Uri(builder.Configuration["PasswordStore:BaseUrl"]
            ?? throw new InvalidOperationException("Missing PasswordStore:BaseUrl"))
    );

builder.Services
    .AddHostedTemporalWorker(
        builder.Configuration["Temporal:TargetHost"]!,
        WorkflowDefinitions.ClientNamespace,
        PublishWebshieldWorkflow.DefaultTaskQueue)
    .ConfigureOptions(options => options.ClientOptions!.Runtime = temporalRuntime)
    .AddScopedActivities<WaasService<WebshieldData>>()
    .AddScopedActivities<WebshieldActivities>()
    .AddWorkflow<PublishWebshieldWorkflow>();

builder.Services.AddHealthChecks()
    .AddWaasDatabaseCheck(waasConnectionString);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
