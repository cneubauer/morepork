namespace WaaS.Common.Workflow;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Extensions.Hosting;
using Temporalio.Runtime;

public static class WaasServiceCollectionExtensions
{
    // One runtime per process: it owns the Prometheus listener that Prometheus scrapes, so the
    // injected client and the hosted worker's client must share it.
    private static readonly Lazy<TemporalRuntime> _temporalRuntime = new(() => new TemporalRuntime(new()
    {
        Telemetry = new() { Metrics = new() { Prometheus = new("0.0.0.0:9000") } },
    }));

    public static string GetWaasConnectionString(this IConfiguration configuration)
        => configuration.GetConnectionString("WaaS")
            ?? throw new InvalidOperationException("Missing connection string for WaaS");

    /// <summary>
    /// Registers what every WaaS process needs: stack instance, notification and tenant stores,
    /// the password service client and the database readiness check.
    /// </summary>
    public static IServiceCollection AddWaasCore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetWaasConnectionString();

        services
            .AddScoped<IStackInstanceStore>(serviceProvider => new StackInstanceStore(connectionString))
            .AddScoped<INotificationStore>(serviceProvider => new NotificationStore(connectionString))
            .AddTenantStore(connectionString);

        services.AddHttpClient<IPasswordService, PasswordService>(
            client => client.BaseAddress = new Uri(configuration["PasswordStore:BaseUrl"]
                ?? throw new InvalidOperationException("Missing PasswordStore:BaseUrl"))
        );

        services.AddHealthChecks()
            .AddWaasDatabaseCheck(connectionString);

        return services;
    }

    public static IServiceCollection AddWaasTemporalClient(this IServiceCollection services, IConfiguration configuration)
        => services.AddTemporalClient(options =>
        {
            options.TargetHost = configuration["Temporal:TargetHost"];
            options.Runtime = _temporalRuntime.Value;
        });

    public static ITemporalWorkerServiceOptionsBuilder AddWaasTemporalWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        string taskQueue)
        => services
            .AddHostedTemporalWorker(configuration["Temporal:TargetHost"]!, WorkflowDefinitions.ClientNamespace, taskQueue)
            .ConfigureOptions(options => options.ClientOptions!.Runtime = _temporalRuntime.Value);
}
