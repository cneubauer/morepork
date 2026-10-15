namespace WaaS.Space.Classic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Extensions.Hosting;
using WaaS.Space.Classic.DesiredState;
using WaaS.Space.Classic.Workflow;
using WaaS.Space.Workflow;
using WebspaceMiddleware;

public static class ClassicServiceCollectionExtensions
{
    /// <summary>
    /// Everything <see cref="ClassicWebspaceController"/> needs. Requires <c>AddWaasCore</c> and
    /// <c>AddWaasTemporalClient</c>.
    /// </summary>
    public static IServiceCollection AddClassicWebspaceApi(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddDesiredStateStore<SharedWebspaceData>(configuration.GetWaasConnectionString())
            .AddScoped<WaasService<SharedWebspaceData>>();

    /// <summary>
    /// The Temporal worker for <see cref="PublishClassicWebspaceWorkflow"/>. Requires <c>AddWaasCore</c>.
    /// </summary>
    public static IServiceCollection AddClassicWebspaceWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDesiredStateStore<SharedWebspaceData>(configuration.GetWaasConnectionString());

        services.AddHttpClient<ISpaceMiddlewareService<SharedWebspaceData, Webspace>, WebspaceMiddlewareService>(
            client => client.BaseAddress = new Uri(configuration["WebspaceMiddleware:BaseUrl"]
                ?? throw new InvalidOperationException("Missing WebspaceMiddleware:BaseUrl"))
        );

        services
            .AddWaasTemporalWorker(configuration, PublishClassicWebspaceWorkflow.DefaultTaskQueue)
            .AddScopedActivities<WaasService<SharedWebspaceData>>()
            .AddScopedActivities<ClassicWebspaceService>()
            .AddScopedActivities<PasswordService>()
            .AddWorkflow<PublishClassicWebspaceWorkflow>();

        return services;
    }
}
