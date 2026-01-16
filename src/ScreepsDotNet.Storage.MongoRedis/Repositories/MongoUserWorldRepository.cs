using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Comparers;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserWorldRepository(IMongoDatabaseProvider databaseProvider) : IUserWorldRepository
{
    private static readonly string ControllerType = RoomObjectType.Controller.ToDocumentValue();
    private static readonly string SpawnType = RoomObjectType.Spawn.ToDocumentValue();

    private readonly IMongoCollection<RoomObjectDocument> _collection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);

    public async Task<RoomReference?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rooms = await GetControllerRoomReferencesAsync(userId, sort: null, cancellationToken).ConfigureAwait(false);
        if (rooms.Count == 0)
            return null;

        var index = Random.Shared.Next(rooms.Count);
        return rooms[index];
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
    private sealed record RoomProjection(string? Room, string? Shard);

    public async Task<IReadOnlyCollection<RoomReference>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var sort = Builders<RoomObjectDocument>.Sort.Descending(document => document.Level);
        return await GetControllerRoomReferencesAsync(userId, sort, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<RoomReference>> GetControllerRoomReferencesAsync(string userId,
                                                                            SortDefinition<RoomObjectDocument>? sort,
                                                                            CancellationToken cancellationToken)
    {
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(document => document.UserId, userId),
            Builders<RoomObjectDocument>.Filter.Eq(document => document.Type, ControllerType));

        var query = _collection.Find(filter);
        if (sort is not null)
            query = query.Sort(sort);

        var rooms = await query.Project(document => new RoomProjection(document.Room, document.Shard))
                               .ToListAsync(cancellationToken)
                               .ConfigureAwait(false);

        return rooms.Where(room => !string.IsNullOrWhiteSpace(room.Room))
                    .Select(room => RoomReference.Create(room.Room!, room.Shard))
                    .Distinct(RoomReferenceComparer.OrdinalIgnoreCase)
                    .ToList();
    }
}
