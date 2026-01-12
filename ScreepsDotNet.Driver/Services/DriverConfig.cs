using System.Collections.ObjectModel;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Customization;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Eventing;

namespace ScreepsDotNet.Driver.Services;

internal sealed class DriverConfig : IDriverConfig
{
    private readonly IEnvironmentService _environment;
    private readonly Lock _prototypeLock = new();
    private readonly List<CustomObjectPrototype> _prototypes = [];
    private IReadOnlyCollection<CustomObjectPrototype> _prototypeSnapshot = [];

    private readonly Lock _intentLock = new();
    private readonly Dictionary<string, CustomIntentDefinition> _intentDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, CustomIntentDefinition> _intentSnapshot =
        new ReadOnlyDictionary<string, CustomIntentDefinition>(new Dictionary<string, CustomIntentDefinition>(StringComparer.OrdinalIgnoreCase));

    private int _mainLoopMinDurationMs;
    private int _mainLoopResetIntervalMs;
    private int _cpuMaxPerTick;
    private int _cpuBucketSize;
    private int _historyChunkSize;
    private bool _useSigintTimeout;
    private bool _enableInspector;

    public DriverConfig(IEnvironmentService environmentService)
    {
        _environment = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _mainLoopMinDurationMs = LoadInt(_environment.GetMainLoopMinDurationAsync, 1000);
        _mainLoopResetIntervalMs = LoadInt(_environment.GetMainLoopResetIntervalAsync, 5000);
        _cpuMaxPerTick = LoadInt(_environment.GetCpuMaxPerTickAsync, 500);
        _cpuBucketSize = LoadInt(_environment.GetCpuBucketSizeAsync, 10_000);
        _historyChunkSize = LoadInt(_environment.GetHistoryChunkSizeAsync, 20);
        _useSigintTimeout = LoadBool(_environment.GetUseSigintTimeoutAsync, false);
        _enableInspector = LoadBool(_environment.GetEnableInspectorAsync, false);
    }

    public event EventHandler<LoopStageEventArgs>? MainLoopStage;
    public event EventHandler<LoopStageEventArgs>? RunnerLoopStage;
    public event EventHandler<LoopStageEventArgs>? ProcessorLoopStage;
    public event EventHandler<PlayerSandboxEventArgs>? PlayerSandbox;
    public event EventHandler<DriverInitEventArgs>? Initialized;
    public event EventHandler<RoomHistorySavedEventArgs>? RoomHistorySaved;

    public int MainLoopMinDurationMs
    {
        get => _mainLoopMinDurationMs;
        set => SetInt(ref _mainLoopMinDurationMs, value, _environment.SetMainLoopMinDurationAsync);
    }

    public int MainLoopResetIntervalMs
    {
        get => _mainLoopResetIntervalMs;
        set => SetInt(ref _mainLoopResetIntervalMs, value, _environment.SetMainLoopResetIntervalAsync);
    }

    public int CpuMaxPerTick
    {
        get => _cpuMaxPerTick;
        set => SetInt(ref _cpuMaxPerTick, value, _environment.SetCpuMaxPerTickAsync);
    }

    public int CpuBucketSize
    {
        get => _cpuBucketSize;
        set => SetInt(ref _cpuBucketSize, value, _environment.SetCpuBucketSizeAsync);
    }

    public int HistoryChunkSize
    {
        get => _historyChunkSize;
        set => SetInt(ref _historyChunkSize, value, _environment.SetHistoryChunkSizeAsync);
    }

    public bool UseSigintTimeout
    {
        get => _useSigintTimeout;
        set => SetBool(ref _useSigintTimeout, value, _environment.SetUseSigintTimeoutAsync);
    }

    public bool EnableInspector
    {
        get => _enableInspector;
        set => SetBool(ref _enableInspector, value, _environment.SetEnableInspectorAsync);
    }

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

    public void EmitRoomHistorySaved(RoomHistorySavedEventArgs args) =>
        RoomHistorySaved?.Invoke(this, args);

    private static int LoadInt(Func<CancellationToken, Task<int?>> loader, int fallback)
    {
        try
        {
            var value = loader(CancellationToken.None).GetAwaiter().GetResult();
            return value ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool LoadBool(Func<CancellationToken, Task<bool?>> loader, bool fallback)
    {
        try
        {
            var value = loader(CancellationToken.None).GetAwaiter().GetResult();
            return value ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void SetInt(ref int field, int value, Func<int, CancellationToken, Task> setter)
    {
        if (field == value) return;
        field = value;
        setter(value, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void SetBool(ref bool field, bool value, Func<bool, CancellationToken, Task> setter)
    {
        if (field == value) return;
        field = value;
        setter(value, CancellationToken.None).GetAwaiter().GetResult();
    }
}
