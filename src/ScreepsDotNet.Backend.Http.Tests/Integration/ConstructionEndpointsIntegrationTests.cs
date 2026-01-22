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
public sealed class ConstructionEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
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
    public async Task CreateConstruction_Success()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 2);

        var request = new PlaceConstructionRequest(room, 25, 25, StructureType.Extension, null);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateConstruction, request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        Assert.NotNull(content.GetProperty("_id").GetString());

        // Verify database state
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var site = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("type", "constructionSite")).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(site);
        Assert.Equal(StructureType.Extension.ToDocumentValue(), site["structureType"].AsString);
        Assert.Equal(SeedDataDefaults.User.Id, site["user"].AsString);
        Assert.Equal(3000, site["progressTotal"].AsInt32);
    }

    [Fact]
    public async Task CreateConstruction_RclNotEnough_ReturnsError()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 1);

        // Spawn already exists at RCL 1, adding another one should fail
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.InsertOneAsync(new BsonDocument
        {
            ["type"] = StructureType.Spawn.ToDocumentValue(),
            ["room"] = room,
            ["x"] = 10,
            ["y"] = 10,
            ["user"] = SeedDataDefaults.User.Id
        }, cancellationToken: TestContext.Current.CancellationToken);

        var request = new PlaceConstructionRequest(room, 25, 25, StructureType.Spawn, "Spawn2");
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateConstruction, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("RCL not enough", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CreateConstruction_InvalidLocation_ReturnsError()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 2);

        // Attempt to build on the controller itself
        // Move controller to non-border position to avoid border check interfering with occupancy error message
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var updateResult = await objectsCollection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", "controller"),
                Builders<BsonDocument>.Filter.Eq("room", room)
            ),
            Builders<BsonDocument>.Update.Set("x", 10).Set("y", 10),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(1, updateResult.MatchedCount);

        var request = new PlaceConstructionRequest(room, 10, 10, StructureType.Extension, null);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateConstruction, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("Position blocked", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CreateConstruction_WithShard_Success()
    {
        var token = await LoginAsync();
        var room = SeedDataDefaults.World.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 2, shard);

        var request = new PlaceConstructionRequest(room, 15, 15, StructureType.Road, null, shard);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateConstruction, request);
        response.EnsureSuccessStatusCode();

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("shard", shard),
            Builders<BsonDocument>.Filter.Eq("type", "constructionSite"));
        var site = await objectsCollection.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(site);
        Assert.Equal(shard, site["shard"].AsString);
    }

    private async Task PrepareRoomWithControllerAsync(string room, string userId, int level, string? shard = null)
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
            ["x"] = 1,
            ["y"] = 1,
            ["level"] = level,
            ["user"] = userId
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
}
