using ScreepsDotNet.Driver.Abstractions.Customization;
using ScreepsDotNet.Driver.Abstractions.Eventing;

namespace ScreepsDotNet.Driver.Abstractions.Config;

public interface IDriverConfig
{
    event EventHandler<LoopStageEventArgs>? MainLoopStage;
    event EventHandler<LoopStageEventArgs>? RunnerLoopStage;
    event EventHandler<LoopStageEventArgs>? ProcessorLoopStage;
    event EventHandler<PlayerSandboxEventArgs>? PlayerSandbox;
    event EventHandler<DriverInitEventArgs>? Initialized;

    int MainLoopMinDurationMs { get; set; }
    int MainLoopResetIntervalMs { get; set; }
    int CpuMaxPerTick { get; set; }
    int CpuBucketSize { get; set; }
    int HistoryChunkSize { get; set; }
    bool UseSigintTimeout { get; set; }
    bool EnableInspector { get; set; }

    IReadOnlyCollection<CustomObjectPrototype> CustomObjectPrototypes { get; }
    IReadOnlyDictionary<string, CustomIntentDefinition> CustomIntentTypes { get; }

    void RegisterCustomObjectPrototype(CustomObjectPrototype prototype);
    void RegisterCustomIntent(CustomIntentDefinition intentDefinition);

    void EmitMainLoopStage(string stage, object? payload = null);
    void EmitRunnerLoopStage(string stage, object? payload = null);
    void EmitProcessorLoopStage(string stage, object? payload = null);
    void EmitPlayerSandbox(PlayerSandboxEventArgs args);
    void EmitInitialized(DriverProcessType processType);
}
