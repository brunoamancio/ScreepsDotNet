namespace ScreepsDotNet.Engine;

using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Driver.Abstractions.Engine;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.GlobalState;
using ScreepsDotNet.Engine.Data.Memory;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Host;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.GlobalSteps;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Telemetry;
using ScreepsDotNet.Engine.Validation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEngineCore(this IServiceCollection services)
    {
        services.AddIntentValidation();

        // Telemetry
        services.AddSingleton<IEngineTelemetrySink, EngineTelemetrySink>();

        services.AddSingleton<IRoomStateProvider, RoomStateProvider>();
        services.AddSingleton<IGlobalStateProvider, GlobalStateProvider>();
        services.AddSingleton<IRoomMutationWriterFactory, RoomMutationWriterFactory>();
        services.AddSingleton<IUserMemorySink, UserMemorySink>();

        // CRITICAL: IntentValidationStep MUST run FIRST before all other steps
        services.AddSingleton<IRoomProcessorStep, IntentValidationStep>();

        services.AddSingleton<IRoomProcessorStep, CreepLifecycleStep>();
        services.AddSingleton<IRoomProcessorStep, MovementIntentStep>();
        services.AddSingleton<IRoomProcessorStep, CreepSayIntentStep>();
        services.AddSingleton<IRoomProcessorStep, CreepSuicideIntentStep>();
        services.AddSingleton<IRoomProcessorStep, SpawnIntentStep>();
        services.AddSingleton<IRoomProcessorStep, SpawnSpawningStep>();
        services.AddSingleton<IRoomProcessorStep, TowerIntentStep>();
        services.AddSingleton<IRoomProcessorStep, CreepBuildRepairStep>();
        services.AddSingleton<IRoomProcessorStep, HarvestIntentStep>();
        services.AddSingleton<IRoomProcessorStep, SourceRegenerationStep>();
        services.AddSingleton<IRoomProcessorStep, MineralRegenerationStep>();
        services.AddSingleton<IRoomProcessorStep, KeeperLairStep>();
        services.AddSingleton<IRoomProcessorStep, KeeperAiStep>();
        services.AddSingleton<IRoomProcessorStep, InvaderAiStep>();
        services.AddSingleton<IRoomProcessorStep, ResourceTransferIntentStep>();
        services.AddSingleton<IRoomProcessorStep, LabIntentStep>();
        services.AddSingleton<IRoomProcessorStep, LinkIntentStep>();
        services.AddSingleton<IRoomProcessorStep, PowerSpawnIntentStep>();
        services.AddSingleton<IRoomProcessorStep, NukerIntentStep>();
        services.AddSingleton<IRoomProcessorStep, FactoryIntentStep>();
        services.AddSingleton<IRoomProcessorStep, CombatResolutionStep>();
        services.AddSingleton<IRoomProcessorStep, StructureDecayStep>();
        services.AddSingleton<IRoomProcessorStep, ControllerDowngradeStep>();
        services.AddSingleton<IRoomProcessorStep, ControllerIntentStep>();
        services.AddSingleton<IRoomProcessorStep, PowerEffectDecayStep>();
        services.AddSingleton<IRoomProcessorStep, PowerAbilityStep>();
        services.AddSingleton<IRoomProcessorStep, PowerAbilityCooldownStep>();
        services.AddSingleton<IRoomProcessorStep, NukeLandingStep>();
        services.AddSingleton<IRoomProcessorStep, RoomIntentEventLogStep>();
        services.AddSingleton<IBodyAnalysisHelper, BodyAnalysisHelper>();
        services.AddSingleton<ISpawnStateReader, SpawnStateReader>();
        services.AddSingleton<ISpawnEnergyAllocator, SpawnEnergyAllocator>();
        services.AddSingleton<ISpawnEnergyCharger, SpawnEnergyCharger>();
        services.AddSingleton<ISpawnIntentParser, SpawnIntentParser>();
        services.AddSingleton<IResourceDropHelper, ResourceDropHelper>();
        services.AddSingleton<ICreepDeathProcessor, CreepDeathProcessor>();
        services.AddSingleton<IStructureBlueprintProvider, StructureBlueprintProvider>();
        services.AddSingleton<IStructureSnapshotFactory, StructureSnapshotFactory>();
        services.AddSingleton<IRoomProcessor, RoomProcessor>();
        services.AddSingleton<IGlobalProcessor, EngineGlobalProcessor>();
        services.AddSingleton<IGlobalProcessorStep, InterRoomTransferStep>();
        services.AddSingleton<IGlobalProcessorStep, PowerCreepIntentStep>();
        services.AddSingleton<IGlobalProcessorStep, MarketIntentStep>();
        services.AddSingleton<IGlobalMutationWriterFactory, GlobalMutationWriterFactory>();
        services.AddSingleton<IEngineHost, EngineHost>();
        return services;
    }
}
