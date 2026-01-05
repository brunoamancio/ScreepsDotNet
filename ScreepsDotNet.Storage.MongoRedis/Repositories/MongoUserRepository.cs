using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserRepository : IUserRepository
{
    private const string CpuField = "cpu";
    private const string ActiveField = "active";
    private const string BotField = "bot";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Ne(ActiveField, 0),
                                                       Builders<BsonDocument>.Filter.Gt(CpuField, 0),
                                                       Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Exists(BotField, false),
                                                                                        Builders<BsonDocument>.Filter.Eq(BotField, BsonNull.Value)));

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (int)count;
    }
}
