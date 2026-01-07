namespace ScreepsDotNet.Storage.MongoRedis.Services;

using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;

public sealed class MongoUserRespawnService(IMongoDatabaseProvider databaseProvider, IUserWorldRepository userWorldRepository) : IUserRespawnService
{
    private const string UserField = "user";
    private const string IdField = "_id";
    private const string LastRespawnField = "lastRespawnDate";

    private readonly IMongoCollection<BsonDocument> _roomsObjectsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<BsonDocument> _usersCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<UserRespawnResult> RespawnAsync(string userId, CancellationToken cancellationToken = default)
    {
        var status = await userWorldRepository.GetWorldStatusAsync(userId, cancellationToken).ConfigureAwait(false);
        if (status is not UserWorldStatus.Normal and not UserWorldStatus.Lost)
            return UserRespawnResult.InvalidStatus;

        var userFilter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var userExists = await _usersCollection.Find(userFilter)
                                               .AnyAsync(cancellationToken)
                                               .ConfigureAwait(false);

        if (!userExists)
            return UserRespawnResult.UserNotFound;

        var roomFilter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        await _roomsObjectsCollection.DeleteManyAsync(roomFilter, cancellationToken).ConfigureAwait(false);

        var update = Builders<BsonDocument>.Update.Set(LastRespawnField, DateTime.UtcNow);
        await _usersCollection.UpdateOneAsync(userFilter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

        return UserRespawnResult.Success;
    }
}
