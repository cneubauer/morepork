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

var waasConnectionString = builder.Configuration.GetConnectionString("WaaS")
    ?? throw new InvalidOperationException("Missing connection string for WaaS");

builder.Services.AddScoped<IStackInstanceStore>(
    serviceProvider => new StackInstanceStore(waasConnectionString)
);

builder.Services.AddDesiredStateStore<SharedWebspaceData>(waasConnectionString);

builder.Services.AddTenantStore(
    builder.Configuration.GetConnectionString("TenantStore")
    ?? throw new InvalidOperationException("Missing connection string for TenantStore")
);

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
