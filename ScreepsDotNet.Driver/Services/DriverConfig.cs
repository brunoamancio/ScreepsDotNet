using System.Collections.ObjectModel;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Customization;
using ScreepsDotNet.Driver.Abstractions.Eventing;

namespace ScreepsDotNet.Driver.Services;

internal sealed class DriverConfig : IDriverConfig
{
    private readonly Lock _prototypeLock = new();
    private readonly List<CustomObjectPrototype> _prototypes = [];
    private IReadOnlyCollection<CustomObjectPrototype> _prototypeSnapshot = [];

    private readonly Lock _intentLock = new();
    private readonly Dictionary<string, CustomIntentDefinition> _intentDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, CustomIntentDefinition> _intentSnapshot =
        new ReadOnlyDictionary<string, CustomIntentDefinition>(new Dictionary<string, CustomIntentDefinition>(StringComparer.OrdinalIgnoreCase));

    public event EventHandler<LoopStageEventArgs>? MainLoopStage;
    public event EventHandler<LoopStageEventArgs>? RunnerLoopStage;
    public event EventHandler<LoopStageEventArgs>? ProcessorLoopStage;
    public event EventHandler<PlayerSandboxEventArgs>? PlayerSandbox;
    public event EventHandler<DriverInitEventArgs>? Initialized;

    public int MainLoopMinDurationMs { get; set; } = 1000;
    public int MainLoopResetIntervalMs { get; set; } = 5000;
    public int CpuMaxPerTick { get; set; } = 500;
    public int CpuBucketSize { get; set; } = 10_000;
    public int HistoryChunkSize { get; set; } = 20;
    public bool UseSigintTimeout { get; set; }
    public bool EnableInspector { get; set; }

    public IReadOnlyCollection<CustomObjectPrototype> CustomObjectPrototypes => _prototypeSnapshot;
    public IReadOnlyDictionary<string, CustomIntentDefinition> CustomIntentTypes => _intentSnapshot;

    public void RegisterCustomObjectPrototype(CustomObjectPrototype prototype)
    {
        ArgumentNullException.ThrowIfNull(prototype);

        lock (_prototypeLock)
        {
            _prototypes.Add(prototype);
            _prototypeSnapshot = _prototypes.ToArray();
        }
    }

    public void RegisterCustomIntent(CustomIntentDefinition intentDefinition)
    {
        ArgumentNullException.ThrowIfNull(intentDefinition);

        lock (_intentLock)
        {
            _intentDefinitions[intentDefinition.Name] = intentDefinition;
            _intentSnapshot = new ReadOnlyDictionary<string, CustomIntentDefinition>(new Dictionary<string, CustomIntentDefinition>(_intentDefinitions, StringComparer.OrdinalIgnoreCase));
        }
    }

    public void EmitMainLoopStage(string stage, object? payload = null) =>
        MainLoopStage?.Invoke(this, new LoopStageEventArgs(stage, payload));

    public void EmitRunnerLoopStage(string stage, object? payload = null) =>
        RunnerLoopStage?.Invoke(this, new LoopStageEventArgs(stage, payload));

    public void EmitProcessorLoopStage(string stage, object? payload = null) =>
        ProcessorLoopStage?.Invoke(this, new LoopStageEventArgs(stage, payload));

    public void EmitPlayerSandbox(PlayerSandboxEventArgs args) =>
        PlayerSandbox?.Invoke(this, args);

    public void EmitInitialized(DriverProcessType processType) =>
        Initialized?.Invoke(this, new DriverInitEventArgs(processType));
}
