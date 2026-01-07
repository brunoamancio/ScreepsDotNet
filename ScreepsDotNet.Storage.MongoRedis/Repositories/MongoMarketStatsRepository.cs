namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoMarketStatsRepository(IMongoDatabaseProvider databaseProvider) : IMarketStatsRepository
{
    private readonly IMongoCollection<MarketStatsDocument> _collection = databaseProvider.GetCollection<MarketStatsDocument>(databaseProvider.Settings.MarketStatsCollection);

    public async Task<IReadOnlyList<MarketStatsEntry>> GetStatsAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketStatsDocument>.Filter.Eq(document => document.ResourceType, resourceType);
        var sort = Builders<MarketStatsDocument>.Sort.Descending(document => document.Date);

        var documents = await _collection.Find(filter)
                                         .Sort(sort)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(document => new MarketStatsEntry(document.ResourceType,
                                                                 document.Date,
                                                                 document.Transactions,
                                                                 document.Volume,
                                                                 document.AveragePrice,
                                                                 document.StandardDeviation))
                        .ToList();
    }
}
