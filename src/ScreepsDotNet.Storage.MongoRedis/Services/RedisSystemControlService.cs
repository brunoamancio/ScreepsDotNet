namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Globalization;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

public sealed class RedisSystemControlService(IRedisConnectionProvider connectionProvider, ILogger<RedisSystemControlService> logger)
    : ISystemControlService, IDisposable
{
    private readonly IConnectionMultiplexer _connection = connectionProvider.GetConnection();

    public async Task<bool> IsSimulationPausedAsync(CancellationToken cancellationToken = default)
    {
        var db = _connection.GetDatabase();
        var value = await db.StringGetAsync(SystemControlConstants.MainLoopPausedKey).ConfigureAwait(false);
        return value == "1";
    }

    public Task PauseSimulationAsync(CancellationToken cancellationToken = default)
        => SetPauseStateAsync(paused: true);

    public Task ResumeSimulationAsync(CancellationToken cancellationToken = default)
        => SetPauseStateAsync(paused: false);

    public async Task<int?> GetTickDurationAsync(CancellationToken cancellationToken = default)
    {
        var db = _connection.GetDatabase();
        var value = await db.StringGetAsync(SystemControlConstants.MainLoopMinimumDurationKey).ConfigureAwait(false);
        if (!value.HasValue)
            return null;

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public async Task SetTickDurationAsync(int minimalDurationMilliseconds, CancellationToken cancellationToken = default)
    {
        if (minimalDurationMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimalDurationMilliseconds), "Tick duration must be positive.");

        var db = _connection.GetDatabase();
        var stringValue = minimalDurationMilliseconds.ToString(CultureInfo.InvariantCulture);
        await db.StringSetAsync(SystemControlConstants.MainLoopMinimumDurationKey, stringValue).ConfigureAwait(false);

        var subscriber = _connection.GetSubscriber();
        var channel = new RedisChannel(SystemControlConstants.TickRateChannel, RedisChannel.PatternMode.Literal);
        await subscriber.PublishAsync(channel, stringValue).ConfigureAwait(false);
    }

    public async Task PublishServerMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must be provided.", nameof(message));

        var subscriber = _connection.GetSubscriber();
        var channel = new RedisChannel(SystemControlConstants.ServerMessageChannel, RedisChannel.PatternMode.Literal);
        await subscriber.PublishAsync(channel, message).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_connection is IDisposable disposable)
            disposable.Dispose();
    }

    private async Task SetPauseStateAsync(bool paused)
    {
        var db = _connection.GetDatabase();
        await db.StringSetAsync(SystemControlConstants.MainLoopPausedKey, paused ? "1" : "0").ConfigureAwait(false);
        logger.LogInformation("Simulation {State}.", paused ? "paused" : "resumed");
    }
}
