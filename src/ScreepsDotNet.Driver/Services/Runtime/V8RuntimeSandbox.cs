using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Common;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class V8RuntimeSandbox(RuntimeSandboxOptions options, ILogger<V8RuntimeSandbox>? logger = null) : IRuntimeSandbox
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly RuntimeSandboxOptions _options = options;
    private readonly ILogger<V8RuntimeSandbox>? _logger = logger;

    public Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modules = ExtractModules(context);
        var script = modules is null ? ExtractScript(context) : null;
        using var engine = CreateEngine();
        var cpuLimitMs = Math.Max(context.CpuLimit, _options.DefaultCpuLimitMs) + _options.ScriptInterruptBufferMs;
        using var cpuTimeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(cpuLimitMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cpuTimeoutCts.Token);
        using var registration = linkedCts.Token.Register(engine.Interrupt);

        var initialMemoryJson = SerializeMemory(context.Memory);
        var bridge = new ScriptBridge(initialMemoryJson, context.MemorySegments ?? new Dictionary<int, string>(), context.InterShardSegment);
        engine.AddHostObject("host", HostItemFlags.PrivateAccess, bridge);
        engine.Execute(ScriptPrelude);

        engine.Script.GameTime = context.GameTime;
        engine.Script.UserId = context.UserId;
        engine.Script.CodeHash = context.CodeHash;
        engine.Script.CpuLimit = Math.Max(context.CpuLimit, _options.DefaultCpuLimitMs);

        engine.Execute($"var Memory = {initialMemoryJson};");

        var stopwatch = Stopwatch.StartNew();
        string? error = null;
        try
        {
            if (modules is { Count: > 0 })
                ExecuteModuleGraph(engine, modules);
            else if (!string.IsNullOrWhiteSpace(script))
                engine.Execute(script);
        }
        catch (ScriptInterruptedException)
        {
            error = "Script execution timed out.";
            _logger?.LogWarning("Runtime interrupted for user {UserId}.", context.UserId);
        }
        catch (ScriptEngineException ex)
        {
            error = ex.Message;
            _logger?.LogError(ex, "Runtime error for user {UserId}.", context.UserId);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _logger?.LogError(ex, "Unexpected runtime failure for user {UserId}.", context.UserId);
        }
        stopwatch.Stop();

        var memoryResult = engine.Evaluate("typeof Memory === 'undefined' ? null : JSON.stringify(Memory)") as string;
        var finalMemory = bridge.GetRawMemoryOverride()
                           ?? (memoryResult is not null && !string.Equals(memoryResult, initialMemoryJson, StringComparison.Ordinal)
                               ? memoryResult
                               : null);
        var updatedSegments = bridge.GetUpdatedMemorySegments();
        var interShardSegment = bridge.GetInterShardSegmentOverride();

        return Task.FromResult(new RuntimeExecutionResult(
            bridge.ConsoleLog,
            bridge.ConsoleResults,
            error,
            bridge.GetGlobalIntents(),
            finalMemory,
            updatedSegments,
            interShardSegment,
            (int)Math.Clamp(Math.Ceiling(stopwatch.Elapsed.TotalMilliseconds), 0, int.MaxValue),
            bridge.GetRoomIntents(),
            bridge.GetNotifications()));
    }

    private V8ScriptEngine CreateEngine()
    {
        var engine = new V8ScriptEngine(V8ScriptEngineFlags.DisableGlobalMembers, 64);
        var heapSize = _options.MaxHeapSizeMegabytes;
        if (heapSize > 0)
        {
            var bytes = checked((ulong)heapSize * 1024UL * 1024UL);
            engine.MaxRuntimeHeapSize = checked((UIntPtr)bytes);
        }

        engine.RuntimeHeapSizeSampleInterval = TimeSpan.FromMilliseconds(50);
        return engine;
    }

    private static void ExecuteModuleGraph(V8ScriptEngine engine, IReadOnlyDictionary<string, string> modules)
    {
        var registry = new ModuleRegistry(modules);
        engine.AddHostObject("moduleRegistry", registry);
        engine.Execute(ModuleLoaderPrelude);
    }

    private static IReadOnlyDictionary<string, string>? ExtractModules(RuntimeExecutionContext context)
    {
        if (!context.RuntimeData.TryGetValue("modules", out var value) || value is null)
            return null;

        static IReadOnlyDictionary<string, string>? FromEnumerable(IEnumerable<KeyValuePair<string, string>> source)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, code) in source)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                dict[name] = code;
            }

            return dict.Count == 0 ? null : dict;
        }

        return value switch
        {
            IReadOnlyDictionary<string, string> { Count: > 0 } typed => typed,
            IDictionary<string, string> { Count: > 0 } dict => new Dictionary<string, string>(dict, StringComparer.Ordinal),
            IEnumerable<KeyValuePair<string, string>> enumerable => FromEnumerable(enumerable),
            JsonElement { ValueKind: JsonValueKind.Object } element => FromEnumerable(element.EnumerateObject()
                                                                                             .Select(prop => new KeyValuePair<string, string>(
                                                                                                              prop.Name, prop.Value.ValueKind == JsonValueKind.String
                                                                                                                  ? prop.Value.GetString() ?? string.Empty
                                                                                                                  : prop.Value.GetRawText()))),
            _ => null
        };
    }

    private static string SerializeMemory(IReadOnlyDictionary<string, object?>? memory)
    {
        if (memory is null || memory.Count == 0)
            return "{}";
        return JsonSerializer.Serialize(memory, JsonOptions);
    }

    private static string ExtractScript(RuntimeExecutionContext context)
    {
        if (context.RuntimeData.TryGetValue("script", out var scriptValue) && scriptValue is string script && !string.IsNullOrWhiteSpace(script))
            return script;

        throw new InvalidOperationException("RuntimeData.script is required to execute user code.");
    }

    private const string ModuleLoaderPrelude = """
const __driverModuleCache = Object.create(null);
const __driverModuleFactories = Object.create(null);

function __driverCompileModule(name) {
    let factory = __driverModuleFactories[name];
    if (factory) {
        return factory;
    }
    const source = moduleRegistry.GetSource(name);
    if (typeof source !== "string") {
        throw new Error("Module '" + name + "' not found.");
    }
    factory = new Function("module", "exports", "require", source);
    __driverModuleFactories[name] = factory;
    return factory;
}

function __driverRequire(name) {
    if (!name) {
        throw new Error("Module name is required.");
    }
    if (__driverModuleCache[name]) {
        return __driverModuleCache[name].exports;
    }
    const module = { exports: {} };
    __driverModuleCache[name] = module;
    const factory = __driverCompileModule(name);
    factory(module, module.exports, __driverRequire);
    return module.exports;
}

globalThis.require = __driverRequire;
const __driverMainModule = __driverRequire("main");
if (__driverMainModule && typeof __driverMainModule.loop === "function") {
    __driverMainModule.loop();
}
""";

    private sealed class ModuleRegistry(IReadOnlyDictionary<string, string> modules)
    {
        private readonly IReadOnlyDictionary<string, string> _modules = modules;

        [ScriptMember("GetSource")]
        public string? GetSource(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return _modules.TryGetValue(name, out var code) ? code : null;
        }
    }

    private const string ScriptPrelude = """
const __driverHost = host;
function __driverStringify(value) {
    try {
        if (typeof value === 'string') {
            return value;
        }
        return JSON.stringify(value);
    } catch (err) {
        return value !== undefined && value !== null ? value.toString() : '';
    }
}
const console = {
    log: (...args) => __driverHost.Log(args.map(__driverStringify).join(' ')),
    result: (...args) => __driverHost.Result(args.map(__driverStringify).join(' ')),
    error: (...args) => __driverHost.Error(args.map(__driverStringify).join(' '))
};
function registerIntent(type, payload) {
    if (!type) {
        return;
    }
    const body = payload === undefined ? null : JSON.stringify(payload);
    __driverHost.SetIntent(type, body);
}
function notify(message, groupIntervalMinutes = 0) {
    __driverHost.Notify(message, groupIntervalMinutes || 0);
}
const RawMemory = (() => {
    let raw = __driverHost.GetRawMemory();
    const segmentsSourceJson = __driverHost.GetMemorySegments();
    const segmentsTarget = segmentsSourceJson ? JSON.parse(segmentsSourceJson) : Object.create(null);
    const segmentsProxy = new Proxy(segmentsTarget, {
        set(target, prop, value) {
            const index = Number(prop);
            if (!Number.isFinite(index) || !Number.isInteger(index)) {
                return false;
            }
            const text = value == null ? '' : String(value);
            target[prop] = text;
            __driverHost.SetMemorySegment(index, text);
            return true;
        },
        deleteProperty(target, prop) {
            const index = Number(prop);
            if (!Number.isFinite(index) || !Number.isInteger(index)) {
                return false;
            }
            delete target[prop];
            __driverHost.DeleteMemorySegment(index);
            return true;
        }
    });
    return {
        get() { return raw; },
        set(value) {
            raw = value == null ? '' : String(value);
            __driverHost.SetRawMemory(raw);
        },
        get segments() { return segmentsProxy; },
        set segments(value) {
            if (!value || typeof value !== 'object') {
                return;
            }
            Object.keys(value).forEach(key => {
                segmentsProxy[key] = value[key];
            });
        },
        get interShardSegment() { return __driverHost.GetInterShardSegment(); },
        set interShardSegment(value) {
            __driverHost.SetInterShardSegment(value == null ? '' : String(value));
        }
    };
})();
""";

    private sealed class ScriptBridge
    {
        private readonly List<string> _consoleLog = [];
        private readonly List<string> _consoleResults = [];
        private readonly Dictionary<string, object?> _globalIntents = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, object?>> _roomIntents = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<NotifyIntentPayload> _notifications = [];
        private readonly string _initialRawMemory;
        private readonly Dictionary<int, string> _initialSegments;
        private readonly Dictionary<int, string> _segmentUpdates = [];
        private readonly string? _initialInterShardSegment;
        private string _currentRawMemory;
        private string? _rawMemoryOverride;
        private string? _interShardOverride;

        public ScriptBridge(string initialRawMemory, IReadOnlyDictionary<int, string> initialSegments, string? interShardSegment)
        {
            _initialRawMemory = initialRawMemory ?? "{}";
            _currentRawMemory = _initialRawMemory;
            _initialSegments = initialSegments is { Count: > 0 }
                ? new Dictionary<int, string>(initialSegments)
                : new Dictionary<int, string>();
            _initialInterShardSegment = interShardSegment;

        }

        public IReadOnlyList<string> ConsoleLog => _consoleLog;
        public IReadOnlyList<string> ConsoleResults => _consoleResults;

        public IReadOnlyDictionary<string, object?> GetGlobalIntents()
            => new Dictionary<string, object?>(_globalIntents, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> GetRoomIntents()
        {
            var snapshot = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (room, intents) in _roomIntents)
                snapshot[room] = new Dictionary<string, object?>(intents, StringComparer.OrdinalIgnoreCase);
            return snapshot;
        }

        public IReadOnlyList<NotifyIntentPayload> GetNotifications() => _notifications.ToArray();

        public string? GetRawMemoryOverride() => _rawMemoryOverride;

        public IReadOnlyDictionary<int, string>? GetUpdatedMemorySegments()
            => _segmentUpdates.Count == 0 ? null : new Dictionary<int, string>(_segmentUpdates);

        public string? GetInterShardSegmentOverride() => _interShardOverride;

        [ScriptMember("Log")]
        public void Log(object? message)
        {
            if (message is null) return;
            _consoleLog.Add(message.ToString() ?? string.Empty);
        }

        [ScriptMember("Result")]
        public void Result(object? message)
        {
            if (message is null) return;
            _consoleResults.Add(message.ToString() ?? string.Empty);
        }

        [ScriptMember("Error")]
        public void Error(object? message)
        {
            if (message is null) return;
            _consoleLog.Add($"ERROR: {message}");
        }

        [ScriptMember("SetIntent")]
        public void SetIntent(object? intentType, object? payloadJson)
        {
            var typeText = intentType?.ToString();
            if (string.IsNullOrWhiteSpace(typeText))
                return;

            var payload = ParsePayload(payloadJson);
            var roomName = TryExtractRoomName(payload);
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                var intents = GetOrCreateRoomIntent(roomName);
                intents[typeText] = payload;
                return;
            }

            _globalIntents[typeText] = payload;
        }

        [ScriptMember("Notify")]
        public void Notify(object? message, object? intervalMinutes)
        {
            var text = message?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var minutes = intervalMinutes switch
            {
                null => 0,
                int value => value,
                long value => (int)Math.Clamp(value, int.MinValue, int.MaxValue),
                double value => (int)Math.Round(value),
                float value => (int)Math.Round(value),
                string value when int.TryParse(value, out var parsed) => parsed,
                _ => 0
            };

            _notifications.Add(new NotifyIntentPayload(text.Trim(), Math.Max(0, minutes)));
        }

        [ScriptMember("GetRawMemory")]
        public string GetRawMemory() => _currentRawMemory;

        [ScriptMember("SetRawMemory")]
        public void SetRawMemory(object? value)
        {
            _currentRawMemory = NormalizeString(value);
            _rawMemoryOverride = string.Equals(_currentRawMemory, _initialRawMemory, StringComparison.Ordinal)
                ? null
                : _currentRawMemory;
        }

        [ScriptMember("GetMemorySegments")]
        public string GetMemorySegments()
        {
            if (_initialSegments.Count == 0)
                return "{}";

            var map = new Dictionary<string, string>(_initialSegments.Count, StringComparer.Ordinal);
            foreach (var (index, value) in _initialSegments)
                map[index.ToString(CultureInfo.InvariantCulture)] = value ?? string.Empty;
            return JsonSerializer.Serialize(map, JsonOptions);
        }

        [ScriptMember("SetMemorySegment")]
        public void SetMemorySegment(object? indexValue, object? segmentValue)
        {
            if (!TryParseSegmentIndex(indexValue, out var index))
                return;

            var text = NormalizeSegment(segmentValue);
            if (_initialSegments.TryGetValue(index, out var initialValue) && string.Equals(initialValue ?? string.Empty, text, StringComparison.Ordinal))
                _segmentUpdates.Remove(index);
            else
                _segmentUpdates[index] = text;
        }

        [ScriptMember("DeleteMemorySegment")]
        public void DeleteMemorySegment(object? indexValue)
        {
            if (!TryParseSegmentIndex(indexValue, out var index))
                return;

            if (_initialSegments.ContainsKey(index))
                _segmentUpdates[index] = string.Empty;
            else
                _segmentUpdates.Remove(index);
        }

        [ScriptMember("GetInterShardSegment")]
        public string GetInterShardSegment()
            => _interShardOverride ?? _initialInterShardSegment ?? string.Empty;

        [ScriptMember("SetInterShardSegment")]
        public void SetInterShardSegment(object? value)
        {
            var text = NormalizeString(value);
            if (string.Equals(text, _initialInterShardSegment ?? string.Empty, StringComparison.Ordinal))
                _interShardOverride = null;
            else
                _interShardOverride = text;
        }

        private static object? ParsePayload(object? payloadJson)
        {
            var payloadText = payloadJson?.ToString();
            if (string.IsNullOrWhiteSpace(payloadText))
                return null;

            try
            {
                var parsed = JsonSerializer.Deserialize<object>(payloadText, JsonOptions);
                if (parsed is JsonElement { ValueKind: JsonValueKind.Object })
                    return JsonSerializer.Deserialize<Dictionary<string, object?>>(payloadText, JsonOptions);
                return parsed;
            }
            catch
            {
                return payloadText;
            }
        }

        private Dictionary<string, object?> GetOrCreateRoomIntent(string roomName)
        {
            if (_roomIntents.TryGetValue(roomName, out var intents))
                return intents;

            intents = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _roomIntents[roomName] = intents;
            return intents;
        }

        private static string? TryExtractRoomName(object? payload)
        {
            string? Resolve(object? candidate)
            {
                var text = candidate?.ToString()?.Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }

            return payload switch
            {
                IDictionary<string, object?> dictionary when dictionary.TryGetValue(IntentPayloadFields.Room, out var roomValue) => Resolve(roomValue),
                JsonElement { ValueKind: JsonValueKind.Object } element when element.TryGetProperty(IntentPayloadFields.Room, out var roomProperty) && roomProperty.ValueKind == JsonValueKind.String => Resolve(roomProperty.GetString()),
                _ => null
            };
        }

        private static bool TryParseSegmentIndex(object? value, out int index)
        {
            switch (value)
            {
                case null:
                    index = 0;
                    return false;
                case int i:
                    index = i;
                    return true;
                case long l when l is >= int.MinValue and <= int.MaxValue:
                    index = (int)l;
                    return true;
                case double d when Math.Abs(d % 1) < double.Epsilon:
                    index = (int)d;
                    return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    index = parsed;
                    return true;
                default:
                    index = 0;
                    return false;
            }
        }

        private static string NormalizeString(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string text => text,
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string NormalizeSegment(object? value)
            => NormalizeString(value);
    }
}
