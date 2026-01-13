namespace ScreepsDotNet.Engine;

using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalState;
using ScreepsDotNet.Engine.Data.Memory;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Steps;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEngineCore(this IServiceCollection services)
    {
        services.AddSingleton<IRoomStateProvider, RoomStateProvider>();
        services.AddSingleton<IGlobalStateProvider, GlobalStateProvider>();
        services.AddSingleton<IRoomMutationWriterFactory, RoomMutationWriterFactory>();
        services.AddSingleton<IUserMemorySink, UserMemorySink>();
        services.AddSingleton<IRoomProcessorStep, CreepLifecycleStep>();
        services.AddSingleton<IRoomProcessorStep, StructureDecayStep>();
        services.AddSingleton<IRoomProcessorStep, ControllerDowngradeStep>();
        services.AddSingleton<IRoomProcessorStep, RoomIntentEventLogStep>();
        services.AddSingleton<IRoomProcessor, RoomProcessor>();
        return services;
    }
}
