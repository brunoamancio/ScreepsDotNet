namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Services;
using StackExchange.Redis;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

public sealed class SystemCommandsIntegrationFixture : IAsyncLifetime
{
    private const string MongoImage = "mongo:7.0";
    private const string RedisImage = "redis:7.2";

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder(MongoImage).Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder(RedisImage).Build();
    private readonly ISeedDataService _seedDataService = new SeedDataService();

    private MongoClient? _mongoClient;
    private MongoRedisStorageOptions _options = null!;
    private RedisConnectionProvider _redisProvider = null!;
    private IMongoDatabaseProvider _databaseProvider = null!;

    public IMongoDatabase Database { get; private set; } = null!;
    public MongoRedisStorageOptions StorageOptions => _options;
    public IMongoDatabaseProvider DatabaseProvider => _databaseProvider;
    public IRedisConnectionProvider RedisProvider => _redisProvider;
    public IConnectionMultiplexer RedisConnection => _redisProvider.GetConnection();

    public async ValueTask InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        await _redisContainer.StartAsync();

        _options = new MongoRedisStorageOptions
        {
            MongoConnectionString = _mongoContainer.GetConnectionString(),
            MongoDatabase = SeedDataDefaults.Database.Name,
            RedisConnectionString = _redisContainer.GetConnectionString()
        };

        _mongoClient = new MongoClient(_options.MongoConnectionString);
        Database = _mongoClient.GetDatabase(_options.MongoDatabase);
        _databaseProvider = new MongoDatabaseProvider(Options.Create(_options));

        _redisProvider = new RedisConnectionProvider(Options.Create(_options));

        await ResetStateAsync().ConfigureAwait(false);
    }

    public async Task ResetStateAsync()
    {
        await _seedDataService.ReseedAsync(Database).ConfigureAwait(false);
        var db = RedisConnection.GetDatabase();
        await db.ExecuteAsync("FLUSHALL").ConfigureAwait(false);
    }

    public ISystemControlService CreateSystemControlService()
        => new RedisSystemControlService(_redisProvider, NullLogger<RedisSystemControlService>.Instance);

    public IMongoCollection<TDocument> GetCollection<TDocument>(string name)
        => Database.GetCollection<TDocument>(name);

    public async ValueTask DisposeAsync()
    {
        _redisProvider.Dispose();
        await _mongoContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
