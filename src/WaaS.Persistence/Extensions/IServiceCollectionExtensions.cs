using Microsoft.Extensions.DependencyInjection;

namespace WaaS.Persistence;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDesiredStateStore<TDesiredState>(this IServiceCollection services, string connectionString)
        where TDesiredState : class, IDesiredStateData, new()
    {
        SqlMapper.AddTypeHandler(new JsonTypeHandler<TDesiredState>());

        services.AddScoped<IDesiredStateStore<TDesiredState>>(serviceProvider => new DesiredStateStore<TDesiredState>(connectionString));

        return services;
    }

    public static IServiceCollection AddTenantStore(this IServiceCollection services, string connectionString)
    {
        SqlMapper.AddTypeHandler(new JsonTypeHandler<TenantProfile>());

        services.AddScoped<ITenantStore>(serviceProvider => new TenantStore(connectionString));

        return services;
    }
}
