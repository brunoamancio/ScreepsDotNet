namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Invader;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using ScreepsDotNet.Storage.MongoRedis.Services;

[Trait("Category", "Integration")]
public sealed class InvaderCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task InvaderCreateCommand_CreatesInvader()
    {
        await fixture.ResetAsync();
        var roomName = "W1N1";
        var token = TestContext.Current.CancellationToken;
        await PrepareRoomWithControllerAsync(roomName, SeedDataDefaults.User.Id, 3, token: token);

        var service = CreateInvaderService();
        var command = new InvaderCreateCommand(service, fixture.DatabaseProvider.GetCollection<UserDocument>("users").ToUserRepository());
        var settings = new InvaderCreateCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            RoomName = roomName,
            X = 25,
            Y = 25,
            Type = InvaderType.Melee,
            Size = InvaderSize.Small,
            Boosted = false
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var objectsCollection = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var invaderExists = await objectsCollection
            .Find(doc => doc["room"] == roomName && doc["type"] == StructureType.Creep.ToDocumentValue() && doc["user"] == SeedDataDefaults.World.InvaderUser)
            .AnyAsync(token);
        Assert.True(invaderExists);
    }

    [Fact]
    public async Task InvaderCreateCommand_WithShard_PersistsShard()
    {
        await fixture.ResetAsync();
        var roomName = SeedDataDefaults.World.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;
        var token = TestContext.Current.CancellationToken;
        await PrepareRoomWithControllerAsync(roomName, SeedDataDefaults.User.Id, 3, token, shard);

        var service = CreateInvaderService();
        var command = new InvaderCreateCommand(service, fixture.DatabaseProvider.GetCollection<UserDocument>("users").ToUserRepository());
        var settings = new InvaderCreateCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            RoomName = roomName,
            Shard = shard,
            X = 10,
            Y = 10,
            Type = InvaderType.Ranged,
            Size = InvaderSize.Small,
            Boosted = false
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var objectsCollection = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var invaderFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", roomName),
            Builders<BsonDocument>.Filter.Eq("shard", shard),
            Builders<BsonDocument>.Filter.Eq("type", StructureType.Creep.ToDocumentValue()));
        var invader = await objectsCollection.Find(invaderFilter).FirstOrDefaultAsync(token);
        Assert.NotNull(invader);
        Assert.Equal(shard, invader["shard"].AsString);
    }

    private async Task PrepareRoomWithControllerAsync(string room, string userId, int level, CancellationToken token, string? shard = null)
    {
        var roomsCollection = fixture.Database.GetCollection<BsonDocument>("rooms");
        var roomDoc = new BsonDocument { ["_id"] = room, ["status"] = "normal" };
        if (!string.IsNullOrWhiteSpace(shard))
            roomDoc["shard"] = shard;
        await roomsCollection.ReplaceOneAsync(new BsonDocument { ["_id"] = room }, roomDoc, new ReplaceOptions { IsUpsert = true }, token);

        var terrainCollection = fixture.Database.GetCollection<BsonDocument>("rooms.terrain");
        var terrainDoc = new BsonDocument { ["room"] = room, ["terrain"] = new string('0', 2500) };
        if (!string.IsNullOrWhiteSpace(shard))
            terrainDoc["shard"] = shard;
        await terrainCollection.ReplaceOneAsync(new BsonDocument { ["room"] = room }, terrainDoc, new ReplaceOptions { IsUpsert = true }, token);

        var objectsCollection = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var controllerDoc = new BsonDocument
        {
            ["type"] = "controller",
            ["room"] = room,
            ["x"] = 1,
            ["y"] = 1,
            ["level"] = level,
            ["user"] = userId
        };
        if (!string.IsNullOrWhiteSpace(shard))
            controllerDoc["shard"] = shard;
        await objectsCollection.InsertOneAsync(controllerDoc, cancellationToken: token);
    }

    private MongoInvaderService CreateInvaderService()
        => new(fixture.DatabaseProvider, NullLogger<MongoInvaderService>.Instance);
}

internal static class UserRepositoryExtensions
{
    public static Core.Repositories.IUserRepository ToUserRepository(this IMongoCollection<UserDocument> collection)
        => new Storage.MongoRedis.Repositories.MongoUserRepository(new SimpleDatabaseProvider(collection));

    private sealed class SimpleDatabaseProvider(IMongoCollection<UserDocument> collection) : Storage.MongoRedis.Providers.IMongoDatabaseProvider
    {
        public Storage.MongoRedis.Options.MongoRedisStorageOptions Settings => new() { UsersCollection = "users", RoomObjectsCollection = "rooms.objects", RoomsCollection = "rooms" };
        public IMongoDatabase GetDatabase() => collection.Database;
        public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName) => collection.Database.GetCollection<TDocument>(collectionName);
    }
}
