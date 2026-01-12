using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeCoordinator(
    IMongoDatabaseProvider databaseProvider,
    IRedisConnectionProvider redisProvider,
    IUserDataService userDataService,
    IRuntimeService runtimeService,
    IRuntimeBundleCache bundleCache,
    IRuntimeWatchdog runtimeWatchdog,
    IDriverLoopHooks loopHooks,
    IDriverConfig config,
    IEnvironmentService environmentService,
    ILogger<RuntimeCoordinator>? logger = null) : IRuntimeCoordinator
{
    private const string DefaultBranch = "default";

    private static readonly JsonSerializerOptions MemoryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly IMongoCollection<UserCodeDocument> _userCode =
        databaseProvider.GetCollection<UserCodeDocument>(databaseProvider.Settings.UserCodeCollection);

    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();

    public async Task ExecuteAsync(string userId, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var user = await userDataService.GetUserAsync(userId, token).ConfigureAwait(false);
        if (user is null)
        {
            logger?.LogWarning("Skipping runtime execution because user {UserId} was not found.", userId);
            return;
        }

        var codeDocument = await LoadActiveCodeAsync(userId, token).ConfigureAwait(false);
        if (codeDocument?.Modules is not { Count: > 0 })
        {
            logger?.LogDebug("User {UserId} has no active code modules.", userId);
            return;
        }

        var modules = RuntimeModuleBuilder.NormalizeModules(codeDocument.Modules);
        if (modules.Count == 0 || !modules.ContainsKey("main"))
        {
            logger?.LogDebug("User {UserId} does not have a valid 'main' module.", userId);
            return;
        }

        var codeHash = RuntimeModuleBuilder.ComputeCodeHash(modules);
        var bundle = bundleCache.GetOrAdd(codeHash, modules);
        var gameTime = await environmentService.GetGameTimeAsync(token).ConfigureAwait(false);
        var cpuBucket = await environmentService.GetCpuBucketSizeAsync(token).ConfigureAwait(false) ?? config.CpuBucketSize;

        var forceColdSandbox = runtimeWatchdog.TryConsumeColdStartRequest(userId);

        var context = new RuntimeExecutionContext(
            userId,
            codeHash,
            ResolveCpuLimit(user),
            cpuBucket,
            gameTime,
            await LoadMemoryAsync(userId).ConfigureAwait(false),
            await LoadMemorySegmentsAsync(userId).ConfigureAwait(false),
            await LoadInterShardSegmentAsync(userId).ConfigureAwait(false),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["script"] = bundle.Script,
                ["modules"] = bundle.Modules,
                ["userCodeTimestamp"] = codeDocument.Timestamp,
                ["branch"] = codeDocument.Branch
            },
            forceColdSandbox);

        RuntimeExecutionResult result;
        try
        {
            result = await runtimeService.ExecuteAsync(context, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Runtime execution failed for user {UserId}.", userId);
            var message = ex.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(message))
                await loopHooks.PublishConsoleErrorAsync(userId, message!, token).ConfigureAwait(false);
            throw;
        }

        var telemetry = new RuntimeTelemetryPayload(
            userId,
            gameTime,
            context.CpuLimit,
            cpuBucket,
            result.CpuUsed,
            result.Metrics.TimedOut,
            result.Metrics.ScriptError,
            result.Metrics.HeapUsedBytes,
            result.Metrics.HeapSizeLimitBytes,
            result.Error);

        await loopHooks.PublishRuntimeTelemetryAsync(telemetry, token).ConfigureAwait(false);
        config.EmitRuntimeTelemetry(new RuntimeTelemetryEventArgs(telemetry));

        var replenishedBucket = Math.Clamp(cpuBucket - result.CpuUsed + context.CpuLimit, 0, config.CpuBucketSize);
        await environmentService.SetCpuBucketSizeAsync(replenishedBucket, token).ConfigureAwait(false);

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
            logger?.LogWarning("User {UserId} memory blob is not valid JSON. Resetting to empty object.", userId);
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
        if (result.Memory is not null)
            await userDataService.SaveUserMemoryAsync(userId, result.Memory, token).ConfigureAwait(false);

        if (result.MemorySegments is { Count: > 0 })
            await userDataService.SaveUserMemorySegmentsAsync(userId, result.MemorySegments, token).ConfigureAwait(false);

        if (result.InterShardSegment is not null)
            await userDataService.SaveUserInterShardSegmentAsync(userId, result.InterShardSegment, token).ConfigureAwait(false);

        if (result.RoomIntents.Count > 0 || result.GlobalIntents.Count > 0 || result.Notifications.Count > 0)
        {
            var rooms = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (room, intents) in result.RoomIntents)
                rooms[room] = new Dictionary<string, object?>(intents, StringComparer.OrdinalIgnoreCase);

            var payload = new UserIntentWritePayload(rooms, result.Notifications, result.GlobalIntents);
            await userDataService.SaveUserIntentsAsync(userId, payload, token).ConfigureAwait(false);
        }

        if (result.ConsoleLog.Count > 0 || result.ConsoleResults.Count > 0)
        {
            var payload = new ConsoleMessagesPayload(result.ConsoleLog, result.ConsoleResults);
            await loopHooks.PublishConsoleMessagesAsync(userId, payload, token).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
            await loopHooks.PublishConsoleErrorAsync(userId, result.Error!, token).ConfigureAwait(false);
    }

    private int ResolveCpuLimit(UserDocument user)
    {
        var cpu = user.Cpu ?? config.CpuMaxPerTick;
        var limit = (int)Math.Ceiling(cpu);
        return limit > 0 ? limit : config.CpuMaxPerTick;
    }
}
