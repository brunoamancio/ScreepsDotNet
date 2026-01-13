namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Services;
using Testcontainers.MongoDb;

public sealed class MongoMapIntegrationFixture : IAsyncLifetime
{
    private const string MongoImage = "mongo:7.0";

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder(MongoImage).Build();
    private readonly ISeedDataService _seedDataService = new SeedDataService();

    private MongoClient? _client;
    private MongoRedisStorageOptions _options = null!;
    private IMongoDatabaseProvider _databaseProvider = null!;

    public IMongoDatabase Database { get; private set; } = null!;

    public IMongoDatabaseProvider DatabaseProvider => _databaseProvider;

    public MongoMapControlService MapControlService { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        var connectionString = _mongoContainer.GetConnectionString();
        _client = new MongoClient(connectionString);
        Database = _client.GetDatabase(SeedDataDefaults.Database.Name);
        _options = new MongoRedisStorageOptions
        {
            MongoConnectionString = connectionString,
            MongoDatabase = SeedDataDefaults.Database.Name
        };
        _databaseProvider = new MongoDatabaseProvider(Options.Create(_options));
        MapControlService = new MongoMapControlService(_databaseProvider, NullLogger<MongoMapControlService>.Instance);
        await ResetAsync().ConfigureAwait(false);
    }

    public Task ResetAsync()
        => _seedDataService.ReseedAsync(Database);

    public MongoMapControlService CreateService()
        => new(_databaseProvider, NullLogger<MongoMapControlService>.Instance);

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
        => Database.GetCollection<TDocument>(collectionName);

    public async ValueTask DisposeAsync()
        => await _mongoContainer.DisposeAsync();
}
