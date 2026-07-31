using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WaaS.Persistence;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDesiredStateStore<T>(this IServiceCollection services, string connectionString)
        where T : class, IDesiredStateData, new()
    {
        SqlMapper.AddTypeHandler(new JsonTypeHandler<T>());

        services.AddScoped<IDesiredStateStore<T>>(sp =>
            new DesiredStateStore<T>(connectionString));

        return services;
    }
}
