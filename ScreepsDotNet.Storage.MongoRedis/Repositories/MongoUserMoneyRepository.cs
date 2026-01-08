using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserMoneyRepository(IMongoDatabaseProvider databaseProvider) : IUserMoneyRepository
{
    private readonly IMongoCollection<UserMoneyEntryDocument> _collection = databaseProvider.GetCollection<UserMoneyEntryDocument>(databaseProvider.Settings.UserMoneyCollection);

    public async Task<MoneyHistoryPage> GetHistoryAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserMoneyEntryDocument>.Filter.Eq(document => document.UserId, userId);
        var documents = await _collection.Find(filter)
                                         .Sort(Builders<UserMoneyEntryDocument>.Sort.Descending(document => document.Date))
                                         .Skip(page * pageSize)
                                         .Limit(pageSize + 1)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        var hasMore = documents.Count > pageSize;
        var entries = (hasMore ? documents.Take(pageSize) : documents)
                                          .Select(ToDictionary)
                                          .ToList();

        return new MoneyHistoryPage(page, hasMore, entries);
    }

    private static IReadOnlyDictionary<string, object?> ToDictionary(UserMoneyEntryDocument document)
    {
        var payload = new Dictionary<string, object?>(document.ExtraElements, StringComparer.Ordinal)
        {
            ["user"] = document.UserId,
            ["date"] = document.Date
        };

        return payload;
    }
}
