using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Extensions;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserMoneyRepository : IUserMoneyRepository
{
    private const string UserField = "user";
    private const string DateField = "date";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserMoneyRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UserMoneyCollection);

    public async Task<MoneyHistoryPage> GetHistoryAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var documents = await _collection.Find(filter)
                                         .Sort(Builders<BsonDocument>.Sort.Descending(DateField))
                                         .Skip(page * pageSize)
                                         .Limit(pageSize + 1)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        var hasMore = documents.Count > pageSize;
        var entries = (hasMore ? documents.Take(pageSize) : documents)
            .Select(document => (IReadOnlyDictionary<string, object?>)document.ToPlainDictionary())
            .ToList();

        return new MoneyHistoryPage(page, hasMore, entries);
    }
}
