using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserWorldRepository : IUserWorldRepository
{
    private const string ControllerType = "controller";
    private const string SpawnType = "spawn";

    private readonly IMongoCollection<RoomObjectDocument> _collection;

    public MongoUserWorldRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);

    public async Task<string?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(document => document.UserId, userId),
            Builders<RoomObjectDocument>.Filter.Eq(document => document.Type, ControllerType));

        var rooms = await _collection.Find(filter)
                                     .Project(document => document.Room)
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        var filteredRooms = rooms.Where(room => !string.IsNullOrEmpty(room))
                                 .Select(room => room!)
                                 .ToList();

        if (filteredRooms.Count == 0)
            return null;

        var index = Random.Shared.Next(filteredRooms.Count);
        return filteredRooms[index];
    }

    public async Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var userFilter = Builders<RoomObjectDocument>.Filter.Eq(document => document.UserId, userId);
        var objectsCount = await _collection.CountDocumentsAsync(userFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (objectsCount == 0)
            return UserWorldStatus.Empty;

        var typeFilter = Builders<RoomObjectDocument>.Filter.In(document => document.Type, [SpawnType, ControllerType]);
        var roomObjects = await _collection.Find(Builders<RoomObjectDocument>.Filter.And(userFilter, typeFilter))
                                           .Project(document => new RoomObject(document.Type, document.Room))
                                           .ToListAsync(cancellationToken)
                                           .ConfigureAwait(false);

        var controllerRooms = roomObjects.Where(o => string.Equals(o.Type, ControllerType, StringComparison.Ordinal))
                                         .Select(o => o.Room)
                                         .Where(room => !string.IsNullOrEmpty(room))
                                         .ToHashSet(StringComparer.Ordinal);

        var hasValidSpawn = roomObjects.Where(o => string.Equals(o.Type, SpawnType, StringComparison.Ordinal))
                                       .Select(o => o.Room)
                                      .Any(room => !string.IsNullOrEmpty(room) && controllerRooms.Contains(room));

        return hasValidSpawn ? UserWorldStatus.Normal : UserWorldStatus.Lost;
    }

    private sealed record RoomObject(string? Type, string? Room);

    public async Task<IReadOnlyCollection<string>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(document => document.UserId, userId),
            Builders<RoomObjectDocument>.Filter.Eq(document => document.Type, ControllerType));

        var sort = Builders<RoomObjectDocument>.Sort.Descending(document => document.Level);
        var rooms = await _collection.Find(filter)
                                     .Sort(sort)
                                     .Project(document => document.Room)
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        return rooms.Where(room => !string.IsNullOrEmpty(room))
                    .Select(room => room!)
                    .ToList();
    }
}
