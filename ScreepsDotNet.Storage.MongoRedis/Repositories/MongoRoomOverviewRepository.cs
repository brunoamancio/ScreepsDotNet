namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Common;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoRoomOverviewRepository(IMongoDatabaseProvider databaseProvider) : IRoomOverviewRepository
{
    private readonly IMongoCollection<RoomObjectDocument> _objectsCollection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<UserDocument> _usersCollection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<RoomOverview?> GetRoomOverviewAsync(RoomReference room, CancellationToken cancellationToken = default)
    {
        var controller = await FindControllerAsync(room, cancellationToken).ConfigureAwait(false);
        if (controller is null || string.IsNullOrWhiteSpace(controller.UserId))
            return new RoomOverview(room, null);

        var user = await _usersCollection.Find(Builders<UserDocument>.Filter.Eq(user => user.Id, controller.UserId))
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);

        if (user is null)
            return new RoomOverview(room, null);

        var owner = new RoomOverviewOwner(user.Id!, user.Username ?? string.Empty, user.Badge);
        return new RoomOverview(room, owner);
    }

    private async Task<RoomObjectDocument?> FindControllerAsync(RoomReference room, CancellationToken cancellationToken)
    {
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(obj => obj.Room, room.RoomName),
            Builders<RoomObjectDocument>.Filter.Eq(obj => obj.Type, RoomObjectTypes.Controller));

        if (!string.IsNullOrWhiteSpace(room.ShardName))
            filter &= Builders<RoomObjectDocument>.Filter.Eq(obj => obj.Shard, room.ShardName);

        return await _objectsCollection.Find(filter)
                                       .FirstOrDefaultAsync(cancellationToken)
                                       .ConfigureAwait(false);
    }
}
