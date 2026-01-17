namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Comparers;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoRoomStatusRepository(IMongoDatabaseProvider databaseProvider) : IRoomStatusRepository
{
    private readonly IMongoCollection<RoomDocument> _collection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);

    public async Task<RoomStatusInfo?> GetRoomStatusAsync(string roomName, string? shardName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return null;

        var filter = Builders<RoomDocument>.Filter.Eq(document => document.Id, roomName.Trim());
        if (!string.IsNullOrWhiteSpace(shardName))
            filter &= Builders<RoomDocument>.Filter.Eq(document => document.Shard, shardName.Trim());

        var document = await _collection.Find(filter)
                                        .FirstOrDefaultAsync(cancellationToken)
                                        .ConfigureAwait(false);
        var result = document is null ? null : Convert(document);
        return result;
    }

    public async Task<IReadOnlyDictionary<string, RoomStatusInfo>> GetRoomStatusesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRooms(rooms);
        if (normalized.Count == 0)
            return new Dictionary<string, RoomStatusInfo>(StringComparer.OrdinalIgnoreCase);

        var filter = BuildFilter(normalized);
        var documents = await _collection.Find(filter)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(Convert)
                        .ToDictionary(status => status.RoomName, StringComparer.OrdinalIgnoreCase);
    }

    private static RoomStatusInfo Convert(RoomDocument document)
        => new(document.Id,
               document.Status,
               document.Novice,
               document.RespawnArea,
               document.OpenTime);

    private static IReadOnlyList<RoomReference> NormalizeRooms(IEnumerable<RoomReference> rooms)
        => rooms?.Where(reference => reference is not null && !string.IsNullOrWhiteSpace(reference.RoomName))
                 .Select(reference => RoomReference.Create(reference.RoomName, reference.ShardName))
                 .Distinct(RoomReferenceComparer.OrdinalIgnoreCase)
                 .ToList()
           ?? [];

    private static FilterDefinition<RoomDocument> BuildFilter(IReadOnlyCollection<RoomReference> rooms)
    {
        var filters = new List<FilterDefinition<RoomDocument>>();
        foreach (var group in rooms.GroupBy(reference => reference.RoomName, StringComparer.OrdinalIgnoreCase)) {
            if (group.Any(reference => string.IsNullOrWhiteSpace(reference.ShardName))) {
                filters.Add(Builders<RoomDocument>.Filter.Eq(document => document.Id, group.Key));
                continue;
            }

            var shards = group.Select(reference => reference.ShardName!)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            if (shards.Count == 0) {
                filters.Add(Builders<RoomDocument>.Filter.Eq(document => document.Id, group.Key));
                continue;
            }

            var roomFilter = Builders<RoomDocument>.Filter.Eq(document => document.Id, group.Key);
            var shardFilter = Builders<RoomDocument>.Filter.In(document => document.Shard, shards);
            filters.Add(Builders<RoomDocument>.Filter.And(roomFilter, shardFilter));
        }

        return filters.Count switch
        {
            0 => Builders<RoomDocument>.Filter.Where(_ => false),
            1 => filters[0],
            _ => Builders<RoomDocument>.Filter.Or(filters)
        };
    }

}
