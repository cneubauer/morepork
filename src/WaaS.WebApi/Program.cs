using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddDesiredStateStore<SharedWebspaceData>(
    builder.Configuration.GetConnectionString("WaaS")!);

builder.Services.AddTenantStore(builder.Configuration.GetConnectionString("TenantStore")!);

builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = builder.Configuration["Temporal:TargetHost"];
});

builder.Services.AddHostedService<WorkflowExecutor>();

var app = builder.Build();

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    // Optional configuration for Scalar
    options.WithTitle("WaaS Manager");
});


// app.UseHttpsRedirection();

app.MapControllers();

app.Run();
