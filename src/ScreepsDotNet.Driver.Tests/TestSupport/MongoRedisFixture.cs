using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

namespace ScreepsDotNet.Driver.Tests.TestSupport;

public sealed class MongoRedisFixture : IAsyncLifetime
{
    private const string MongoImage = "mongo:7.0";
    private const string RedisImage = "redis:7.2";
    private readonly MongoDbContainer _mongo = new MongoDbBuilder(MongoImage).Build();
    private readonly RedisContainer _redis = new RedisBuilder(RedisImage).Build();

    private MongoDatabaseProvider _mongoProvider = null!;
    private RedisConnectionProvider _redisProvider = null!;

    public MongoRedisStorageOptions Options { get; private set; } = null!;
    public IMongoDatabaseProvider MongoProvider => _mongoProvider;
    public IRedisConnectionProvider RedisProvider => _redisProvider;

    public IMongoCollection<TDocument> GetCollection<TDocument>(string name)
        => _mongoProvider.GetCollection<TDocument>(name);

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        await _redis.StartAsync();

        Options = new MongoRedisStorageOptions
        {
            MongoConnectionString = _mongo.GetConnectionString(),
            MongoDatabase = $"driver_tests_{Guid.NewGuid():N}",
            RedisConnectionString = _redis.GetConnectionString()
        };

        _mongoProvider = new MongoDatabaseProvider(Options.Create());
        _redisProvider = new RedisConnectionProvider(Options.Create());

        // Ensure collections exist
        var database = _mongoProvider.GetDatabase();
        await database.CreateCollectionAsync(Options.UserNotificationsCollection);
    }

    public async Task DisposeAsync()
    {
        _redisProvider?.Dispose();
        await _mongo.DisposeAsync();
        await _redis.DisposeAsync();
    }
}

internal static class OptionsExtensions
{
    public static IOptions<MongoRedisStorageOptions> Create(this MongoRedisStorageOptions options) =>
        Options.Create(options);
}
