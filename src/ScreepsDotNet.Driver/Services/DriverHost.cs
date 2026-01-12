using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Services;

internal sealed class DriverHost(IServiceProvider serviceProvider, RuntimeTelemetryMonitor telemetryMonitor, RoomHistoryPipeline historyPipeline) : IDriverHost
{
    private readonly RuntimeTelemetryMonitor _telemetryMonitor = telemetryMonitor;
    private readonly RoomHistoryPipeline _historyPipeline = historyPipeline;
    public IDriverConfig Config => serviceProvider.GetRequiredService<IDriverConfig>();
    public IQueueService Queues => GetRequired<IQueueService>();
    public IBulkWriterFactory BulkWriters => GetRequired<IBulkWriterFactory>();
    public IPathfinderService Pathfinder => GetRequired<IPathfinderService>();
    public IRuntimeService Runtime => GetRequired<IRuntimeService>();
    public IRoomDataService Rooms => GetRequired<IRoomDataService>();
    public IUserDataService Users => GetRequired<IUserDataService>();
    public INotificationService Notifications => GetRequired<INotificationService>();
    public IHistoryService History => GetRequired<IHistoryService>();
    public IDriverLoopHooks Loops => GetRequired<IDriverLoopHooks>();
    public IEnvironmentService Environment => serviceProvider.GetRequiredService<IEnvironmentService>();

    public Task ConnectAsync(DriverProcessType processType, CancellationToken token = default)
    {
        Config.EmitInitialized(processType);
        return Task.CompletedTask;
    }

    private T GetRequired<T>() where T : class => serviceProvider.GetRequiredService<T>();
}
