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
    private readonly Lock _eventLock = new();
    private readonly Dictionary<string, List<DriverEventListener>> _emitHandlers = new(StringComparer.OrdinalIgnoreCase);

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
        DispatchEvent(MainLoopStage, "mainLoopStage", new LoopStageEventArgs(stage, payload), stage, payload);

    public void EmitRunnerLoopStage(string stage, object? payload = null) =>
        DispatchEvent(RunnerLoopStage, "runnerLoopStage", new LoopStageEventArgs(stage, payload), stage, payload);

    public void EmitProcessorLoopStage(string stage, object? payload = null) =>
        DispatchEvent(ProcessorLoopStage, "processorLoopStage", new LoopStageEventArgs(stage, payload), stage, payload);

    public void EmitPlayerSandbox(PlayerSandboxEventArgs args) =>
        DispatchEvent(PlayerSandbox, "playerSandbox", args, args);

    public void EmitInitialized(DriverProcessType processType)
    {
        var eventArgs = new DriverInitEventArgs(processType);
        DispatchEvent(Initialized, "init", eventArgs, processType);
    }

    public void EmitRoomHistorySaved(RoomHistorySavedEventArgs args) =>
        DispatchEvent(RoomHistorySaved, "roomHistorySaved", args, args.RoomName, args.BaseGameTime, args.Chunk);

    public IDriverEventSubscription Subscribe(string eventName, DriverEventListener handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_eventLock)
        {
            if (!_emitHandlers.TryGetValue(eventName, out var handlers))
            {
                handlers = [];
                _emitHandlers[eventName] = handlers;
            }

            handlers.Add(handler);
        }

        return new DriverEventSubscription(eventName, handler, this);
    }

    public void Unsubscribe(string eventName, DriverEventListener handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_eventLock)
        {
            if (!_emitHandlers.TryGetValue(eventName, out var handlers))
                return;

            handlers.Remove(handler);
            if (handlers.Count == 0)
                _emitHandlers.Remove(eventName);
        }
    }

    public void Emit(string eventName, params object?[] args)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        DriverEventListener[] snapshot;
        lock (_eventLock)
        {
            if (!_emitHandlers.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
                return;
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler(args);
            }
            catch
            {
                // Swallow to mirror EventEmitter semantics; errors should not break other subscribers.
            }
        }
    }

    private void DispatchEvent<TEventArgs>(EventHandler<TEventArgs>? typedHandler, string eventName, TEventArgs eventArgs, params object?[] emitArgs)
        where TEventArgs : EventArgs
    {
        typedHandler?.Invoke(this, eventArgs);
        Emit(eventName, emitArgs.Length == 0 ? [eventArgs] : emitArgs);
    }

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

    private sealed class DriverEventSubscription(string eventName, DriverEventListener handler, DriverConfig source) : IDriverEventSubscription
    {
        private readonly string _eventName = eventName;
        private readonly DriverEventListener _handler = handler;
        private readonly DriverConfig _source = source;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.Unsubscribe(_eventName, _handler);
        }
    }
}
