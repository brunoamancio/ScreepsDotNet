namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoRoomTerrainRepository(IMongoDatabaseProvider databaseProvider) : IRoomTerrainRepository
{
    private readonly IMongoCollection<RoomTerrainDocument> _collection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);

    public async Task<IReadOnlyList<RoomTerrainData>> GetTerrainEntriesAsync(IEnumerable<string> roomNames, CancellationToken cancellationToken = default)
    {
        var rooms = roomNames.Where(name => !string.IsNullOrWhiteSpace(name))
                             .Select(name => name!)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .ToList();

        var filter = rooms.Count == 0
            ? Builders<RoomTerrainDocument>.Filter.Empty
            : Builders<RoomTerrainDocument>.Filter.In(document => document.Room, rooms);

        var documents = await _collection.Find(filter)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(document => new RoomTerrainData(document.Room, document.Type, document.Terrain))
                        .ToList();
    }
}
