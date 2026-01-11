using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services;

internal sealed class EnvironmentService(IRedisConnectionProvider redisProvider) : IEnvironmentService
{
    private IDatabase Database => redisProvider.GetConnection().GetDatabase();
    private ISubscriber Subscriber => redisProvider.GetConnection().GetSubscriber();

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
}
