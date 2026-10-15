using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

using WaaS.Space.Classic;
using WaaS.WebApi.OpenApi;
using WaaS.Webshield;

var builder = WebApplication.CreateBuilder(args);
var enumNamingPolicy = new LowercaseJsonNamingPolicy();

builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(enumNamingPolicy) }
};

builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer(new SharedWebspaceOperationTransformer(jsonSerializerOptions));
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(enumNamingPolicy));
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(enumNamingPolicy));
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services
    .AddWaasCore(builder.Configuration)
    .AddWaasTemporalClient(builder.Configuration)
    .AddClassicWebspaceApi(builder.Configuration)
    .AddWebshieldApi(builder.Configuration);

builder.Services.AddHostedService<WorkflowExecutor>();

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
