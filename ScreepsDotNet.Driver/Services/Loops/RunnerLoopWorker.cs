using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class RunnerLoopWorker(
    IMongoDatabaseProvider databaseProvider,
    IRedisConnectionProvider redisProvider,
    IUserDataService userDataService,
    IRuntimeService runtimeService,
    IDriverLoopHooks loopHooks,
    IDriverConfig config,
    IEnvironmentService environmentService,
    ILogger<RunnerLoopWorker>? logger = null) : IRunnerLoopWorker
{
    private const string DefaultBranch = "default";

    private static readonly JsonSerializerOptions MemoryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly IMongoCollection<UserCodeDocument> _userCode =
        databaseProvider.GetCollection<UserCodeDocument>(databaseProvider.Settings.UserCodeCollection);

    private readonly IUserDataService _users = userDataService;
    private readonly IRuntimeService _runtime = runtimeService;
    private readonly IDriverLoopHooks _hooks = loopHooks;
    private readonly IDriverConfig _config = config;
    private readonly IEnvironmentService _environment = environmentService;
    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();
    private readonly ILogger<RunnerLoopWorker>? _logger = logger;

    public async Task HandleUserAsync(string userId, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var user = await _users.GetUserAsync(userId, token).ConfigureAwait(false);
        if (user is null)
        {
            _logger?.LogWarning("Skipping runtime execution because user {UserId} was not found.", userId);
            return;
        }

        var codeDocument = await LoadActiveCodeAsync(userId, token).ConfigureAwait(false);
        if (codeDocument?.Modules is not { Count: > 0 })
        {
            _logger?.LogDebug("User {UserId} has no active code modules.", userId);
            return;
        }

        var modules = NormalizeModules(codeDocument.Modules);
        if (modules.Count == 0 || !modules.ContainsKey("main"))
        {
            _logger?.LogDebug("User {UserId} does not have a valid 'main' module.", userId);
            return;
        }
        var script = BuildBundle(modules);

        var gameTime = await _environment.GetGameTimeAsync(token).ConfigureAwait(false);
        var context = new RuntimeExecutionContext(
            userId,
            ComputeCodeHash(codeDocument.Modules),
            ResolveCpuLimit(user),
            _config.CpuBucketSize,
            gameTime,
            await LoadMemoryAsync(userId).ConfigureAwait(false),
            await LoadMemorySegmentsAsync(userId).ConfigureAwait(false),
            await LoadInterShardSegmentAsync(userId).ConfigureAwait(false),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["script"] = script,
                ["modules"] = modules,
                ["userCodeTimestamp"] = codeDocument.Timestamp,
                ["branch"] = codeDocument.Branch
            });

        RuntimeExecutionResult result;
        try
        {
            result = await _runtime.ExecuteAsync(context, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Runtime execution failed for user {UserId}.", userId);
            var message = ex.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(message))
                await _hooks.PublishConsoleErrorAsync(userId, message!, token).ConfigureAwait(false);
            return;
        }

        await PersistResultsAsync(userId, result, token).ConfigureAwait(false);
    }

    private async Task<UserCodeDocument?> LoadActiveCodeAsync(string userId, CancellationToken token)
    {
        var userFilter = Builders<UserCodeDocument>.Filter.Eq(document => document.UserId, userId);
        var activeWorldFilter = Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.ActiveWorld, true));

        var document = await _userCode.Find(activeWorldFilter).FirstOrDefaultAsync(token).ConfigureAwait(false);
        if (document is not null)
            return document;

        var defaultFilter = Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.Branch, DefaultBranch));
        return await _userCode.Find(defaultFilter).FirstOrDefaultAsync(token).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, object?>> LoadMemoryAsync(string userId)
    {
        var value = await _redis.StringGetAsync($"{RedisKeys.Memory}{userId}").ConfigureAwait(false);
        if (!value.HasValue)
            return new Dictionary<string, object?>(StringComparer.Ordinal);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(value.ToString(), MemoryJsonOptions)
                   ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            _logger?.LogWarning("User {UserId} memory blob is not valid JSON. Resetting to empty object.", userId);
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadMemorySegmentsAsync(string userId)
    {
        var entries = await _redis.HashGetAllAsync($"{RedisKeys.MemorySegments}{userId}").ConfigureAwait(false);
        if (entries.Length == 0)
            return new Dictionary<int, string>();

        var segments = new Dictionary<int, string>(entries.Length);
        foreach (var entry in entries)
        {
            if (!entry.Name.HasValue || !int.TryParse(entry.Name.ToString(), out var index))
                continue;
            segments[index] = entry.Value.HasValue ? entry.Value.ToString() : string.Empty;
        }

        return segments;
    }

    private async Task<string?> LoadInterShardSegmentAsync(string userId)
    {
        var value = await _redis.StringGetAsync($"{RedisKeys.PublicMemorySegments}{userId}").ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    private async Task PersistResultsAsync(string userId, RuntimeExecutionResult result, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(result.Memory))
            await _users.SaveUserMemoryAsync(userId, result.Memory!, token).ConfigureAwait(false);

        if (result.MemorySegments is { Count: > 0 })
            await _users.SaveUserMemorySegmentsAsync(userId, result.MemorySegments, token).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.InterShardSegment))
            await _users.SaveUserInterShardSegmentAsync(userId, result.InterShardSegment!, token).ConfigureAwait(false);

        if (result.RoomIntents.Count > 0 || result.GlobalIntents.Count > 0 || result.Notifications.Count > 0)
        {
            var rooms = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (room, intents) in result.RoomIntents)
                rooms[room] = new Dictionary<string, object?>(intents, StringComparer.OrdinalIgnoreCase);

            var payload = new UserIntentWritePayload(rooms, result.Notifications, result.GlobalIntents);
            await _users.SaveUserIntentsAsync(userId, payload, token).ConfigureAwait(false);
        }

        if (result.ConsoleLog.Count > 0 || result.ConsoleResults.Count > 0)
        {
            var payload = new ConsoleMessagesPayload(result.ConsoleLog, result.ConsoleResults);
            await _hooks.PublishConsoleMessagesAsync(userId, payload, token).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
            await _hooks.PublishConsoleErrorAsync(userId, result.Error!, token).ConfigureAwait(false);
    }

    private int ResolveCpuLimit(UserDocument user)
    {
        var cpu = user.Cpu ?? _config.CpuMaxPerTick;
        var limit = (int)Math.Ceiling(cpu);
        return limit > 0 ? limit : _config.CpuMaxPerTick;
    }

    private static string ComputeCodeHash(IReadOnlyDictionary<string, string> modules)
    {
        var builder = new StringBuilder();
        foreach (var (name, code) in modules.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(name);
            builder.Append('\n');
            builder.Append(code);
            builder.Append('\n');
        }

        var buffer = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(buffer));
    }

    private static IReadOnlyDictionary<string, string> NormalizeModules(IReadOnlyDictionary<string, string> modules)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, code) in modules)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            normalized[name.Trim()] = code;
        }

        return normalized;
    }

    private static string BuildBundle(IReadOnlyDictionary<string, string> modules)
    {
        if (modules.Count == 0)
            return string.Empty;

        var ordered = modules.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                             .ToArray();

        if (ordered.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("(function(){");
        builder.AppendLine("const modules = {");
        for (var i = 0; i < ordered.Length; i++)
        {
            var (name, code) = ordered[i];
            builder.Append('"');
            builder.Append(EscapeModuleName(name));
            builder.Append("\":function(module, exports, require){\n");
            builder.AppendLine(code);
            builder.Append('}');
            if (i < ordered.Length - 1)
                builder.Append(',');
            builder.AppendLine();
        }
        builder.AppendLine("};");
        builder.AppendLine("const cache = {};");
        builder.AppendLine("const requireModule = name => {");
        builder.AppendLine("  if(!modules[name]) throw new Error(`Module '${name}' not found.`);");
        builder.AppendLine("  if(!cache[name]) {");
        builder.AppendLine("    const module = { exports: {} };");
        builder.AppendLine("    cache[name] = module;");
        builder.AppendLine("    modules[name](module, module.exports, requireModule);");
        builder.AppendLine("  }");
        builder.AppendLine("  return cache[name].exports;");
        builder.AppendLine("};");
        builder.AppendLine("const mainModule = requireModule('main');");
        builder.AppendLine("if(mainModule && typeof mainModule.loop === 'function')");
        builder.AppendLine("  mainModule.loop();");
        builder.AppendLine("})();");
        return builder.ToString();
    }

    private static string EscapeModuleName(string name) =>
        name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
