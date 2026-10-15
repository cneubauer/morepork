using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using WaaS.Common.Workflow;
using WaaS.Webshield;

var builder = WebApplication.CreateBuilder(args);

// Secrets arrive as one file per key from a mounted Kubernetes Secret, so
// "ConnectionStrings__WaaS" as a filename becomes ConnectionStrings:WaaS.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

// The Temporal client is injected by WebshieldActualStateListener and shares the worker's runtime.
builder.Services
    .AddWaasCore(builder.Configuration)
    .AddWaasTemporalClient(builder.Configuration)
    .AddWebshieldWorker(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
