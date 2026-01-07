using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScreepsDotNet.Storage.MongoRedis.Options;

namespace ScreepsDotNet.Storage.MongoRedis.Providers;

public interface IMongoDatabaseProvider
{
    MongoRedisStorageOptions Settings { get; }

    IMongoDatabase GetDatabase();

    IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName);
}

public sealed class MongoDatabaseProvider : IMongoDatabaseProvider
{
    private readonly Lazy<IMongoDatabase> _databaseFactory;

    public MongoDatabaseProvider(IOptions<MongoRedisStorageOptions> options)
    {
        Settings = options.Value;
        _databaseFactory = new Lazy<IMongoDatabase>(() => {
            var client = new MongoClient(Settings.MongoConnectionString);
            return client.GetDatabase(Settings.MongoDatabase);
        });
    }

    public MongoRedisStorageOptions Settings { get; }

    public IMongoDatabase GetDatabase() => _databaseFactory.Value;

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
        => GetDatabase().GetCollection<TDocument>(collectionName);
}
