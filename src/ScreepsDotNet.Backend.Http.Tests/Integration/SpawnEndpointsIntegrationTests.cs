namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class SpawnEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        return content.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task PlaceSpawn_Success()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithNeutralControllerAsync(room);

        // Ensure user is not already playing by clearing their objects
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("user", SeedDataDefaults.User.Id), cancellationToken: TestContext.Current.CancellationToken);

        var request = new PlaceSpawnRequest(room, 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, content.GetProperty("ok").GetInt32());

        // Verify database state
        // Spawn exists
        var spawn = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", StructureType.Spawn.ToDocumentValue())
        )).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(spawn);
        Assert.Equal(SeedDataDefaults.User.Id, spawn["user"].AsString);
        Assert.Equal(25, spawn["x"].AsInt32);
        Assert.Equal(25, spawn["y"].AsInt32);
        Assert.Equal("MySpawn", spawn["name"].AsString);

        // Controller owned
        var controller = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "controller")
        )).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(controller);
        Assert.Equal(SeedDataDefaults.User.Id, controller["user"].AsString);
        Assert.Equal(1, controller["level"].AsInt32);

        // User active
        var usersCollection = harness.Database.GetCollection<BsonDocument>("users");
        var user = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", SeedDataDefaults.User.Id)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10000, user["active"].AsInt32);
    }

    [Fact]
    public async Task PlaceSpawn_AlreadyPlaying_ReturnsError()
    {
        // Arrange
        var token = await LoginAsync();
        // SeedDataDefaults.User already has objects in SeedDataDefaults.World.StartRoom (seeded by SeedDataService)
        var request = new PlaceSpawnRequest("W1N1", 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("already playing", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PlaceSpawn_Occupied_ReturnsError()
    {
        // Arrange
        var token = await LoginAsync();

        var room = "W2N2";
        await PrepareRoomWithNeutralControllerAsync(room);

        // Place something at 25,25
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.InsertOneAsync(new BsonDocument
        {
            ["type"] = "source",
            ["room"] = room,
            ["x"] = 25,
            ["y"] = 25
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Ensure user is not already playing
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("user", SeedDataDefaults.User.Id), cancellationToken: TestContext.Current.CancellationToken);

        var request = new PlaceSpawnRequest(room, 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("Position is occupied", content.GetProperty("error").GetString());
    }

    private async Task PrepareRoomWithNeutralControllerAsync(string room, string? shard = null)
    {
        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        var roomDoc = new BsonDocument { ["_id"] = room, ["status"] = "normal" };
        if (!string.IsNullOrWhiteSpace(shard))
            roomDoc["shard"] = shard;
        var roomFilter = Builders<BsonDocument>.Filter.Eq("_id", room);
        if (!string.IsNullOrWhiteSpace(shard))
            roomFilter &= Builders<BsonDocument>.Filter.Eq("shard", shard);
        await roomsCollection.ReplaceOneAsync(roomFilter, roomDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);

        var terrainCollection = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        var terrainDoc = new BsonDocument { ["room"] = room, ["terrain"] = new string('0', 2500) };
        if (!string.IsNullOrWhiteSpace(shard))
            terrainDoc["shard"] = shard;
        var terrainFilter = Builders<BsonDocument>.Filter.Eq("room", room);
        if (!string.IsNullOrWhiteSpace(shard))
            terrainFilter &= Builders<BsonDocument>.Filter.Eq("shard", shard);
        await terrainCollection.ReplaceOneAsync(terrainFilter, terrainDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var controllerDoc = new BsonDocument
        {
            ["type"] = "controller",
            ["room"] = room,
            ["x"] = 10,
            ["y"] = 10,
            ["level"] = 0
        };
        if (!string.IsNullOrWhiteSpace(shard))
            controllerDoc["shard"] = shard;

        var controllerFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "controller"));
        if (!string.IsNullOrWhiteSpace(shard))
            controllerFilter &= Builders<BsonDocument>.Filter.Eq("shard", shard);
        await objectsCollection.DeleteManyAsync(controllerFilter, cancellationToken: TestContext.Current.CancellationToken);
        await objectsCollection.InsertOneAsync(controllerDoc, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PlaceSpawn_Cleanup_ConvertsToRuins()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W3N3";
        await PrepareRoomWithNeutralControllerAsync(room);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.InsertOneAsync(new BsonDocument
        {
            ["type"] = StructureType.Extension.ToDocumentValue(),
            ["room"] = room,
            ["x"] = 20,
            ["y"] = 20,
            ["user"] = "other-user",
            ["hits"] = 500,
            ["hitsMax"] = 500
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Use a new user to avoid "already playing"
        // Actually, the CurrentUserAccessor in integration tests likely returns the seeded user ID.
        // Let's clear the seeded user's objects instead.
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("user", SeedDataDefaults.User.Id), cancellationToken: TestContext.Current.CancellationToken);

        var request = new PlaceSpawnRequest(room, 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify ruin exists
        var ruin = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("type", "ruin")).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(ruin);
        Assert.Equal("other-user", ruin["user"].AsString);
        Assert.Equal(StructureType.Extension.ToDocumentValue(), ruin["structure"]["type"].AsString);

        // Verify extension is gone
        var extension = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("type", StructureType.Extension.ToDocumentValue())).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(extension);
    }

    [Fact]
    public async Task PlaceSpawn_WithShardParameter_SetsShardField()
    {
        var token = await LoginAsync();
        var room = SeedDataDefaults.World.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;
        await PrepareRoomWithNeutralControllerAsync(room, shard);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("user", SeedDataDefaults.User.Id), cancellationToken: TestContext.Current.CancellationToken);

        var request = new PlaceSpawnRequest(room, 22, 22, "ShardSpawn", shard);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);
        response.EnsureSuccessStatusCode();

        var spawnFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("shard", shard),
            Builders<BsonDocument>.Filter.Eq("type", StructureType.Spawn.ToDocumentValue()));
        var spawnDoc = await objectsCollection.Find(spawnFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(spawnDoc);
        Assert.Equal(shard, spawnDoc["shard"].AsString);
    }
}
