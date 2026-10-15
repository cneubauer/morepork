namespace WaaS.Webshield;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Extensions.Hosting;
using WaaS.Common.Workflow;
using WaaS.Webshield.DesiredState;
using WaaS.Webshield.Workflow;

public static class WebshieldServiceCollectionExtensions
{
    /// <summary>
    /// Everything <see cref="Api.WebshieldController"/> needs. Requires <c>AddWaasCore</c>.
    /// </summary>
    public static IServiceCollection AddWebshieldApi(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddDesiredStateStore<WebshieldData>(configuration.GetWaasConnectionString())
            .AddScoped<WaasService<WebshieldData>>();

    /// <summary>
    /// The Temporal worker for <see cref="PublishWebshieldWorkflow"/> and the RabbitMQ listener for
    /// actual state. Requires <c>AddWaasCore</c> and <c>AddWaasTemporalClient</c>.
    /// </summary>
    public static IServiceCollection AddWebshieldWorker(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetWaasConnectionString();

        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

        services
            .AddDesiredStateStore<WebshieldData>(connectionString)
            .AddScoped<ISslProxyRepository>(serviceProvider => new SslProxyRepository(connectionString))
            .AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>()
            .AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>()
            .AddHostedService<WebshieldActualStateListener>();

        services
            .AddWaasTemporalWorker(configuration, PublishWebshieldWorkflow.DefaultTaskQueue)
            .AddScopedActivities<WaasService<WebshieldData>>()
            .AddScopedActivities<WebshieldActivities>()
            .AddScopedActivities<PasswordService>()
            .AddWorkflow<PublishWebshieldWorkflow>();

        return services;
    }
}
