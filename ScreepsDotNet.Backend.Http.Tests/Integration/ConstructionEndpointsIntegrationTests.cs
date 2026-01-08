namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class ConstructionEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
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
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        Assert.NotNull(content.GetProperty("_id").GetString());

        // Verify database state
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var site = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("type", "constructionSite")).FirstOrDefaultAsync();
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
        });

        var request = new PlaceConstructionRequest(room, 25, 25, StructureType.Spawn, "Spawn2");
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateConstruction, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
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
            Builders<BsonDocument>.Update.Set("x", 10).Set("y", 10)
        );
        Assert.Equal(1, updateResult.MatchedCount);

        var request = new PlaceConstructionRequest(room, 10, 10, StructureType.Extension, null);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateConstruction, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Position blocked", content.GetProperty("error").GetString());
    }

    private async Task PrepareRoomWithControllerAsync(string room, string userId, int level)
    {
        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        await roomsCollection.InsertOneAsync(new BsonDocument { ["_id"] = room, ["status"] = "normal" });

        var terrainCollection = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        await terrainCollection.InsertOneAsync(new BsonDocument { ["room"] = room, ["terrain"] = new string('0', 2500) });

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.InsertOneAsync(new BsonDocument
        {
            ["type"] = "controller",
            ["room"] = room,
            ["x"] = 1,
            ["y"] = 1,
            ["level"] = level,
            ["user"] = userId
        });
    }
}
