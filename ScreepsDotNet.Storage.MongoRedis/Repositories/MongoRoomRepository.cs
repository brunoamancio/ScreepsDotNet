using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoRoomRepository(IMongoDatabaseProvider databaseProvider) : IRoomRepository
{
    private const string UnknownRoomName = "Unknown";

    private readonly IMongoCollection<RoomDocument> _collection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);

    public async Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default)
    {
        var ownedFilter = Builders<RoomDocument>.Filter.Ne(document => document.Owner, null);
        var documents = await _collection.Find(ownedFilter)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(document => {
            var controllerLevel = document.Controller?.Level ?? 0;
            var energy = document.EnergyAvailable ?? 0;
            return new RoomSummary(document.Name ?? UnknownRoomName,
                                    document.Owner,
                                    controllerLevel,
                                    energy);
        }).ToList();
    }
}
