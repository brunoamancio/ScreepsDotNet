using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Extensions;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserWorldRepository : IUserWorldRepository
{
    private const string UserField = "user";
    private const string TypeField = "type";
    private const string RoomField = "room";
    private const string ControllerType = "controller";
    private const string SpawnType = "spawn";
    private const string LevelField = "level";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserWorldRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);

    public async Task<string?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq(UserField, userId),
                                                       Builders<BsonDocument>.Filter.Eq(TypeField, ControllerType));

        var rooms = await _collection.Find(filter)
                                     .Project(document => document.GetStringOrNull(RoomField))
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        var filteredRooms = rooms.Where(room => !string.IsNullOrEmpty(room)).Select(room => room!).ToList();

        if (filteredRooms.Count == 0)
            return null;

        var index = Random.Shared.Next(filteredRooms.Count);
        return filteredRooms[index];
    }

    public async Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var userFilter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var objectsCount = await _collection.CountDocumentsAsync(userFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (objectsCount == 0)
            return UserWorldStatus.Empty;

        var typeFilter = Builders<BsonDocument>.Filter.In(TypeField, [SpawnType, ControllerType]);
        var documents = await _collection.Find(Builders<BsonDocument>.Filter.And(userFilter, typeFilter))
                                         .Project(document => new RoomObject(document.GetStringOrNull(TypeField) ?? string.Empty,
                                                                             document.GetStringOrNull(RoomField) ?? string.Empty))
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        var controllerRooms = documents.Where(o => o.Type == ControllerType)
                                       .Select(o => o.Room)
                                       .Where(room => !string.IsNullOrEmpty(room))
                                       .ToHashSet(StringComparer.Ordinal);

        var hasValidSpawn = documents.Where(o => o.Type == SpawnType)
                                     .Select(o => o.Room)
                                     .Any(room => !string.IsNullOrEmpty(room) && controllerRooms.Contains(room));

        return hasValidSpawn ? UserWorldStatus.Normal : UserWorldStatus.Lost;
    }

    private sealed record RoomObject(string Type, string Room);

    public async Task<IReadOnlyCollection<string>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq(UserField, userId),
                                                       Builders<BsonDocument>.Filter.Eq(TypeField, ControllerType));

        var sort = Builders<BsonDocument>.Sort.Descending(LevelField);
        var rooms = await _collection.Find(filter).Sort(sort)
                                     .Project(document => document.GetStringOrNull(RoomField))
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        return rooms.Where(room => !string.IsNullOrEmpty(room)).Select(room => room!).ToList();
    }
}
