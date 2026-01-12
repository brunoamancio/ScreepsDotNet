using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services;

internal sealed class EnvironmentService(IRedisConnectionProvider redisProvider) : IEnvironmentService
{
    private IDatabase Database => redisProvider.GetConnection().GetDatabase();
    private ISubscriber Subscriber => redisProvider.GetConnection().GetSubscriber();
    private static readonly string True = bool.TrueString;
    private static readonly string False = bool.FalseString;

    public async Task<int> GetGameTimeAsync(CancellationToken token = default)
    {
        var value = await Database.StringGetAsync(RedisKeys.GameTime).ConfigureAwait(false);
        return ParseInt(value);
    }

    public async Task<int> IncrementGameTimeAsync(CancellationToken token = default)
    {
        var result = await Database.StringIncrementAsync(RedisKeys.GameTime).ConfigureAwait(false);
        return (int)result;
    }

    public async Task NotifyTickStartedAsync(CancellationToken token = default)
    {
        var pausedValue = await Database.StringGetAsync(RedisKeys.MainLoopPaused).ConfigureAwait(false);
        if (IsTruthy(pausedValue)) throw new InvalidOperationException("Simulation paused");

        await Subscriber.PublishAsync(RedisChannel.Literal(RedisChannels.TickStarted), "1").ConfigureAwait(false);
    }

    public Task CommitDatabaseBulkAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task SaveIdleTimeAsync(string componentName, TimeSpan duration) => Task.CompletedTask;

    public Task PublishRoomsDoneAsync(int gameTime, CancellationToken token = default) =>
        Subscriber.PublishAsync(RedisChannel.Literal(RedisChannels.RoomsDone), gameTime);

    public Task<int?> GetMainLoopMinDurationAsync(CancellationToken token = default)
        => GetIntValueAsync(RedisKeys.MainLoopMinDuration);

    public Task SetMainLoopMinDurationAsync(int value, CancellationToken token = default)
        => SetIntValueAsync(RedisKeys.MainLoopMinDuration, value);

    public Task<int?> GetMainLoopResetIntervalAsync(CancellationToken token = default)
        => GetIntValueAsync(RedisKeys.MainLoopResetInterval);

    public Task SetMainLoopResetIntervalAsync(int value, CancellationToken token = default)
        => SetIntValueAsync(RedisKeys.MainLoopResetInterval, value);

    public Task<int?> GetCpuMaxPerTickAsync(CancellationToken token = default)
        => GetIntValueAsync(RedisKeys.CpuMaxPerTick);

    public Task SetCpuMaxPerTickAsync(int value, CancellationToken token = default)
        => SetIntValueAsync(RedisKeys.CpuMaxPerTick, value);

    public Task<int?> GetCpuBucketSizeAsync(CancellationToken token = default)
        => GetIntValueAsync(RedisKeys.CpuBucketSize);

    public Task SetCpuBucketSizeAsync(int value, CancellationToken token = default)
        => SetIntValueAsync(RedisKeys.CpuBucketSize, value);

    public Task<int?> GetHistoryChunkSizeAsync(CancellationToken token = default)
        => GetIntValueAsync(RedisKeys.HistoryChunkSize);

    public Task SetHistoryChunkSizeAsync(int value, CancellationToken token = default)
        => SetIntValueAsync(RedisKeys.HistoryChunkSize, value);

    public Task<bool?> GetUseSigintTimeoutAsync(CancellationToken token = default)
        => GetBoolValueAsync(RedisKeys.UseSigintTimeout);

    public Task SetUseSigintTimeoutAsync(bool value, CancellationToken token = default)
        => SetBoolValueAsync(RedisKeys.UseSigintTimeout, value);

    public Task<bool?> GetEnableInspectorAsync(CancellationToken token = default)
        => GetBoolValueAsync(RedisKeys.EnableInspector);

    public Task SetEnableInspectorAsync(bool value, CancellationToken token = default)
        => SetBoolValueAsync(RedisKeys.EnableInspector, value);

    private static int ParseInt(RedisValue value)
    {
        if (!value.HasValue) return 0;

        return int.TryParse(value.ToString(), out var result) ? result : 0;
    }

    private static bool IsTruthy(RedisValue value)
    {
        if (!value.HasValue) return false;

        var text = value.ToString();
        return text is not ("0" or "false");
    }

    private async Task<int?> GetIntValueAsync(string key)
    {
        var value = await Database.StringGetAsync(key).ConfigureAwait(false);
        if (!value.HasValue) return null;
        return int.TryParse(value.ToString(), out var result) ? result : null;
    }

    private Task SetIntValueAsync(string key, int value)
        => Database.StringSetAsync(key, value);

    private async Task<bool?> GetBoolValueAsync(string key)
    {
        var value = await Database.StringGetAsync(key).ConfigureAwait(false);
        if (!value.HasValue) return null;

        var text = value.ToString();
        if (text.Equals(True, StringComparison.OrdinalIgnoreCase) || text == "1") return true;
        if (text.Equals(False, StringComparison.OrdinalIgnoreCase) || text == "0") return false;
        return null;
    }

    private Task SetBoolValueAsync(string key, bool value)
        => Database.StringSetAsync(key, value ? "1" : "0");
}
