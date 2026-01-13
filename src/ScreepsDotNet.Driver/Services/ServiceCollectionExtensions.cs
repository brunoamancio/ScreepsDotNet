using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Services.Bulk;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Services.Loops;
using ScreepsDotNet.Driver.Services.Notifications;
using ScreepsDotNet.Driver.Services.Observability;
using ScreepsDotNet.Driver.Services.Pathfinding;
using ScreepsDotNet.Driver.Services.Queues;
using ScreepsDotNet.Driver.Services.Runtime;
using ScreepsDotNet.Driver.Services.Scheduling;
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
        services.AddSingleton<IRoomSnapshotBuilder, RoomSnapshotBuilder>();
        services.AddSingleton<IRoomSnapshotProvider, RoomSnapshotProvider>();
        services.AddSingleton<IRoomMutationDispatcher, RoomMutationDispatcher>();
        services.AddSingleton<IUserDataService, UserDataService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IPathfinderService, PathfinderService>();
        services.AddSingleton<IDriverLoopHooks, DriverLoopHooks>();
        services.AddOptions<RuntimeSandboxOptions>();
        services.AddOptions<MainLoopOptions>();
        services.AddOptions<RunnerLoopOptions>();
        services.AddOptions<ProcessorLoopOptions>();
        services.AddOptions<SchedulerTelemetryOptions>();
        services.AddOptions<RoomsDoneBroadcastOptions>();
        services.AddOptions<ObservabilityOptions>();
        services.AddSingleton<IRuntimeSandboxFactory, V8RuntimeSandboxFactory>();
        services.AddSingleton<IRuntimeSandboxPool, RuntimeSandboxPool>();
        services.AddSingleton<IRuntimeBundleCache, RuntimeBundleCache>();
        services.AddSingleton<IRuntimeTelemetryListener, LoggingRuntimeTelemetryListener>();
        services.AddSingleton<IObservabilityExporter, LoggingObservabilityExporter>();
        services.AddSingleton<IRuntimeTelemetryListener, SchedulerTelemetryListener>();
        services.AddSingleton<IRuntimeTelemetryListener, ObservabilityTelemetryListener>();
        services.AddSingleton<IRuntimeTelemetrySink, RuntimeTelemetryPipeline>();
        services.AddSingleton<IRuntimeThrottleRegistry, RuntimeThrottleRegistry>();
        services.AddSingleton<RoomHistoryPipeline>();
        services.AddSingleton<IRoomsDoneBroadcaster, RoomsDoneBroadcaster>();
        services.AddSingleton<IRuntimeService, RuntimeService>();
        services.AddSingleton<IRuntimeCoordinator, RuntimeCoordinator>();
        services.AddSingleton<RuntimeTelemetryMonitor>();
        services.AddSingleton<IRuntimeWatchdog>(provider => provider.GetRequiredService<RuntimeTelemetryMonitor>());
        services.AddSingleton<IMainLoopGlobalProcessor, MainLoopGlobalProcessor>();
        services.AddSingleton<IRunnerLoopWorker, RunnerLoopWorker>();
        services.AddSingleton<IProcessorLoopWorker, ProcessorLoopWorker>();
        services.AddSingleton<IMainLoop, MainLoop>();
        services.AddSingleton<IRunnerLoop, RunnerLoop>();
        services.AddSingleton<IProcessorLoop, ProcessorLoop>();
        services.AddSingleton<IDriverHost, DriverHost>();
        return services;
    }
}
