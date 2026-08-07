using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

builder.Services.AddOpenApi();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

var waasConnectionString = builder.Configuration.GetConnectionString("WaaS")
    ?? throw new InvalidOperationException("Missing connection string for WaaS");

builder.Services.AddScoped<IStackInstanceStore>(
    serviceProvider => new StackInstanceStore(waasConnectionString)
);
builder.Services.AddDesiredStateStore<SharedWebspaceData>(waasConnectionString);
builder.Services.AddTenantStore(waasConnectionString);

builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = builder.Configuration["Temporal:TargetHost"];
});

builder.Services.AddHostedService<WorkflowExecutor>();

builder.Services.AddHealthChecks()
    .AddWaasDatabaseCheck(waasConnectionString);

var app = builder.Build();

// Liveness excludes every tagged check, so it answers as long as the process is
// running. Readiness runs the "ready"-tagged checks, so a database outage takes
// the pod out of rotation without triggering a restart loop.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    // Optional configuration for Scalar
    options.WithTitle("WaaS Manager");
});


// app.UseHttpsRedirection();

app.MapControllers();

app.Run();
