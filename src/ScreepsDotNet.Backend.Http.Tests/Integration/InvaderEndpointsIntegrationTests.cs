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
public sealed class InvaderEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
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
    public async Task CreateInvader_Success()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 3);

        var request = new CreateInvaderRequest(room, 25, 25, InvaderType.Melee, InvaderSize.Small, false);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateInvader, request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        var invaderId = content.GetProperty("_id").GetString();
        Assert.NotNull(invaderId);

        // Verify database state
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var invader = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(invaderId))).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(invader);
        Assert.Equal(StructureType.Creep.ToDocumentValue(), invader["type"].AsString);
        Assert.Equal(SeedDataDefaults.World.InvaderUser, invader["user"].AsString);
        Assert.Equal(SeedDataDefaults.User.Id, invader["userSummoned"].AsString);
    }

    [Fact]
    public async Task CreateInvader_NotOwned_ReturnsError()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithControllerAsync(room, "other-user", 3);

        var request = new CreateInvaderRequest(room, 25, 25, InvaderType.Melee, InvaderSize.Small, false);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateInvader, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("not owned", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RemoveInvader_Success()
    {
        // Arrange
        var token = await LoginAsync();
        var room = "W1N1";
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 3);

        var createRequest = new CreateInvaderRequest(room, 25, 25, InvaderType.Melee, InvaderSize.Small, false);
        _client.DefaultRequestHeaders.Add("X-Token", token);
        var createResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateInvader, createRequest);
        var createContent = await TestHttpClient.ReadFromJsonAsync<JsonElement>(createResponse);
        var invaderId = createContent.GetProperty("_id").GetString()!;

        var removeRequest = new RemoveInvaderRequest(invaderId);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.RemoveInvader, removeRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, content.GetProperty("ok").GetInt32());

        // Verify database state
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var invader = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(invaderId))).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(invader);
    }

    [Fact]
    public async Task CreateInvader_WithShard_PersistsShardMetadata()
    {
        var token = await LoginAsync();
        var room = SeedDataDefaults.World.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;
        await PrepareRoomWithControllerAsync(room, SeedDataDefaults.User.Id, 3, shard);

        var request = new CreateInvaderRequest(room, 12, 18, InvaderType.Ranged, InvaderSize.Small, false, shard);
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.CreateInvader, request);
        response.EnsureSuccessStatusCode();

        var payload = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        var invaderId = payload.GetProperty("_id").GetString()!;

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var invader = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(invaderId))).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(invader);
        Assert.Equal(shard, invader.GetValue("shard", shard).AsString);
    }

    private async Task PrepareRoomWithControllerAsync(string room, string userId, int level, string? shard = null)
    {
        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        var roomDoc = new BsonDocument { ["_id"] = room, ["status"] = "normal" };
        if (!string.IsNullOrWhiteSpace(shard))
            roomDoc["shard"] = shard;
        await roomsCollection.ReplaceOneAsync(new BsonDocument { ["_id"] = room }, roomDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);

        var terrainCollection = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        var terrainDoc = new BsonDocument { ["room"] = room, ["terrain"] = new string('0', 2500) };
        if (!string.IsNullOrWhiteSpace(shard))
            terrainDoc["shard"] = shard;
        await terrainCollection.ReplaceOneAsync(new BsonDocument { ["room"] = room }, terrainDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);

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

        await objectsCollection.InsertOneAsync(controllerDoc, cancellationToken: TestContext.Current.CancellationToken);
    }
}
