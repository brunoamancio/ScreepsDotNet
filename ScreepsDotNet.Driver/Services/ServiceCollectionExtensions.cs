using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;

namespace ScreepsDotNet.Driver.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDriverCore(this IServiceCollection services)
    {
        services.AddSingleton<IDriverConfig, DriverConfig>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IDriverHost, DriverHost>();
        return services;
    }
}
