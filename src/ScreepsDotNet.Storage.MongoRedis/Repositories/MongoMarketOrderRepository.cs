namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoMarketOrderRepository(IMongoDatabaseProvider databaseProvider) : IMarketOrderRepository
{
    private const decimal PriceScale = 1000m;

    private readonly IMongoCollection<MarketOrderDocument> _collection = databaseProvider.GetCollection<MarketOrderDocument>(databaseProvider.Settings.MarketOrdersCollection);

    public async Task<IReadOnlyList<MarketOrderSummary>> GetActiveOrderIndexAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketOrderDocument>.Filter.Eq(document => document.Active, true);
        var documents = await _collection.Find(filter)
                                         .Project(doc => new { doc.ResourceType, doc.Type })
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Where(doc => !string.IsNullOrWhiteSpace(doc.ResourceType))
                        .GroupBy(doc => doc.ResourceType!, StringComparer.OrdinalIgnoreCase)
                        .Select(group => {
                            var buying = group.Count(doc => string.Equals(doc.Type, MarketOrderTypes.Buy, StringComparison.OrdinalIgnoreCase));
                            var selling = group.Count(doc => string.Equals(doc.Type, MarketOrderTypes.Sell, StringComparison.OrdinalIgnoreCase));
                            return new MarketOrderSummary(group.Key, group.Count(), buying, selling);
                        })
                        .OrderBy(summary => summary.ResourceType, StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    public Task<IReadOnlyList<MarketOrder>> GetActiveOrdersByResourceAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketOrderDocument>.Filter.And(
            Builders<MarketOrderDocument>.Filter.Eq(document => document.Active, true),
            Builders<MarketOrderDocument>.Filter.Eq(document => document.ResourceType, resourceType));

        return QueryOrdersAsync(filter, cancellationToken);
    }

    public Task<IReadOnlyList<MarketOrder>> GetOrdersByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketOrderDocument>.Filter.Eq(document => document.UserId, userId);
        return QueryOrdersAsync(filter, cancellationToken);
    }

    private async Task<IReadOnlyList<MarketOrder>> QueryOrdersAsync(FilterDefinition<MarketOrderDocument> filter, CancellationToken cancellationToken)
    {
        var documents = await _collection.Find(filter)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(ConvertToModel)
                        .Where(order => order is not null)
                        .Select(order => order!)
                        .ToList();
    }

    private static MarketOrder? ConvertToModel(MarketOrderDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.ResourceType) || string.IsNullOrWhiteSpace(document.Type))
            return null;

        var price = document.Price / PriceScale;

        return new MarketOrder(document.Id.ToString(),
                               document.UserId,
                               document.ResourceType,
                               document.Type,
                               document.RoomName,
                               price,
                               document.Amount,
                               document.RemainingAmount,
                               document.TotalAmount,
                               document.CreatedTick,
                               document.CreatedTimestamp);
    }
}
