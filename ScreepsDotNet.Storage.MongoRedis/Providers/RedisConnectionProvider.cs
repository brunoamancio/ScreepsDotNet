using Microsoft.Extensions.Options;
using ScreepsDotNet.Storage.MongoRedis.Options;
using StackExchange.Redis;

namespace ScreepsDotNet.Storage.MongoRedis.Providers;

public interface IRedisConnectionProvider : IDisposable
{
    IConnectionMultiplexer GetConnection();
}

public sealed class RedisConnectionProvider : IRedisConnectionProvider
{
    private readonly Lazy<IConnectionMultiplexer> _connectionFactory;

    public RedisConnectionProvider(IOptions<MongoRedisStorageOptions> options)
    {
        var settings = options.Value;
        _connectionFactory = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(settings.RedisConnectionString));
    }

    public IConnectionMultiplexer GetConnection() => _connectionFactory.Value;

    public void Dispose()
    {
        if (_connectionFactory.IsValueCreated)
            _connectionFactory.Value.Dispose();
    }
}
