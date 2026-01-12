using System.Diagnostics;
using System.Text.Json;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Constants;

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

        var script = ExtractScript(context);
        using var engine = CreateEngine();
        var cpuLimitMs = Math.Max(context.CpuLimit, _options.DefaultCpuLimitMs) + _options.ScriptInterruptBufferMs;
        using var cpuTimeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(cpuLimitMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cpuTimeoutCts.Token);
        using var registration = linkedCts.Token.Register(engine.Interrupt);

        var bridge = new ScriptBridge();
        engine.AddHostObject("host", HostItemFlags.PrivateAccess, bridge);
        engine.Execute(ScriptPrelude);

        engine.Script.GameTime = context.GameTime;
        engine.Script.UserId = context.UserId;
        engine.Script.CodeHash = context.CodeHash;
        engine.Script.CpuLimit = Math.Max(context.CpuLimit, _options.DefaultCpuLimitMs);

        var memoryJson = SerializeMemory(context.Memory);
        engine.Execute($"var Memory = {memoryJson};");

        var stopwatch = Stopwatch.StartNew();
        string? error = null;
        try
        {
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

        return Task.FromResult(new RuntimeExecutionResult(
            bridge.ConsoleLog,
            bridge.ConsoleResults,
            error,
            bridge.GetGlobalIntents(),
            memoryResult,
            context.MemorySegments,
            context.InterShardSegment,
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
""";

    private sealed class ScriptBridge
    {
        private readonly List<string> _consoleLog = [];
        private readonly List<string> _consoleResults = [];
        private readonly Dictionary<string, object?> _globalIntents = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, object?>> _roomIntents = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<NotifyIntentPayload> _notifications = [];

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

        private static object? ParsePayload(object? payloadJson)
        {
            var payloadText = payloadJson?.ToString();
            if (string.IsNullOrWhiteSpace(payloadText))
                return null;

            try
            {
                var parsed = JsonSerializer.Deserialize<object>(payloadText, JsonOptions);
                if (parsed is JsonElement element && element.ValueKind == JsonValueKind.Object)
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
                JsonElement element when element.ValueKind == JsonValueKind.Object && element.TryGetProperty(IntentPayloadFields.Room, out var roomProperty) && roomProperty.ValueKind == JsonValueKind.String => Resolve(roomProperty.GetString()),
                _ => null
            };
        }
    }
}
