using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = "localhost:7233";
});

var app = builder.Build();

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    // Optional configuration for Scalar
    options
        .WithTitle("Document Processing API")
        .WithTheme(ScalarTheme.DeepSpace) // Awesome dark theme
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});


// app.UseHttpsRedirection();

app.MapControllers();

app.Run();
