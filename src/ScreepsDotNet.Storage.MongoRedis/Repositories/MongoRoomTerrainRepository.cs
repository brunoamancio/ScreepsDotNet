namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Comparers;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoRoomTerrainRepository(IMongoDatabaseProvider databaseProvider) : IRoomTerrainRepository
{
    private readonly IMongoCollection<RoomTerrainDocument> _collection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);

    public async Task<IReadOnlyList<RoomTerrainData>> GetTerrainEntriesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRooms(rooms);
        if (normalized.Count == 0)
            return [];

        var filter = BuildFilter(normalized);
        var documents = await _collection.Find(filter)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(document => new RoomTerrainData(document.Room, document.Shard, document.Type, document.Terrain))
                        .ToList();
    }

    private static IReadOnlyList<RoomReference> NormalizeRooms(IEnumerable<RoomReference> rooms)
        => rooms?.Where(reference => reference is not null && !string.IsNullOrWhiteSpace(reference.RoomName))
                 .Select(reference => RoomReference.Create(reference.RoomName, reference.ShardName))
                 .Distinct(RoomReferenceComparer.OrdinalIgnoreCase)
                 .ToList()
           ?? [];

    private static FilterDefinition<RoomTerrainDocument> BuildFilter(IReadOnlyCollection<RoomReference> rooms)
    {
        var filters = new List<FilterDefinition<RoomTerrainDocument>>();
        foreach (var group in rooms.GroupBy(reference => reference.RoomName, StringComparer.OrdinalIgnoreCase)) {
            var shards = group.Where(reference => !string.IsNullOrWhiteSpace(reference.ShardName))
                              .Select(reference => reference.ShardName!)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            if (group.Any(reference => string.IsNullOrWhiteSpace(reference.ShardName))) {
                filters.Add(Builders<RoomTerrainDocument>.Filter.Eq(document => document.Room, group.Key));
                continue;
            }

            if (shards.Count == 0) {
                filters.Add(Builders<RoomTerrainDocument>.Filter.Eq(document => document.Room, group.Key));
                continue;
            }

            var roomFilter = Builders<RoomTerrainDocument>.Filter.Eq(document => document.Room, group.Key);
            var shardFilter = Builders<RoomTerrainDocument>.Filter.In(document => document.Shard, shards);
            filters.Add(Builders<RoomTerrainDocument>.Filter.And(roomFilter, shardFilter));
        }

        return filters.Count switch
        {
            0 => Builders<RoomTerrainDocument>.Filter.Where(_ => false),
            1 => filters[0],
            _ => Builders<RoomTerrainDocument>.Filter.Or(filters)
        };
    }

}
