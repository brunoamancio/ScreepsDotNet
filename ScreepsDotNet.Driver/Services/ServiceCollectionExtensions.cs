using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Services.Bulk;
using ScreepsDotNet.Driver.Services.Queues;
using ScreepsDotNet.Driver.Services.Rooms;
using ScreepsDotNet.Driver.Services.Users;

namespace ScreepsDotNet.Driver.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDriverCore(this IServiceCollection services)
    {
        services.AddSingleton<IDriverConfig, DriverConfig>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IQueueService, QueueService>();
        services.AddSingleton<IBulkWriterFactory, BulkWriterFactory>();
        services.AddSingleton<IRoomDataService, RoomDataService>();
        services.AddSingleton<IUserDataService, UserDataService>();
        services.AddSingleton<IDriverHost, DriverHost>();
        return services;
    }
}
