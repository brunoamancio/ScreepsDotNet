using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoRoomRepository : IRoomRepository
{
    private const string RoomNameField = "name";
    private const string OwnerField = "owner";
    private const string ControllerField = "controller";
    private const string ControllerLevelField = "level";
    private const string EnergyAvailableField = "energyAvailable";
    private const string UnknownRoomName = "Unknown";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoRoomRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomsCollection);

    public async Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default)
    {
        var ownedFilter = Builders<BsonDocument>.Filter.Exists(OwnerField);
        var documents = await _collection.Find(ownedFilter).ToListAsync(cancellationToken);

        return documents.Select(document =>
                                {
                                    var name = document.TryGetValue(RoomNameField, out var nameValue)
                                        ? nameValue.AsString
                                        : UnknownRoomName;

                                    string? owner = null;
                                    if (document.TryGetValue(OwnerField, out var ownerValue) && ownerValue.IsString)
                                        owner = ownerValue.AsString;

                                    var controllerLevel = 0;
                                    if (document.TryGetValue(ControllerField, out var controllerValue) &&
                                        controllerValue.IsBsonDocument &&
                                        controllerValue.AsBsonDocument.TryGetValue(ControllerLevelField, out var levelValue) &&
                                        levelValue.IsNumeric)
                                    {
                                        controllerLevel = levelValue.ToInt32();
                                    }

                                    var energy = document.TryGetValue(EnergyAvailableField, out var energyValue) && energyValue.IsNumeric ? energyValue.ToInt32() : 0;
                                    return new RoomSummary(name, owner, controllerLevel, energy);
                                })
                          .ToList();
    }
}
