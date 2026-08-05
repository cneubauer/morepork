using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddDesiredStateStore<SharedWebspaceData>(
    builder.Configuration.GetConnectionString("DesiredState")!);

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
    options
        .WithTitle("WaaS Manager")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});


// app.UseHttpsRedirection();

app.MapControllers();

app.Run();
