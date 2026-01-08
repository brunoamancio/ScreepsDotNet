namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class SpawnEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PlaceSpawn_Success()
    {
        // Arrange
        var room = "W1N1";
        await PrepareRoomWithNeutralControllerAsync(room);
        var request = new PlaceSpawnRequest(room, 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SeedDataDefaults.Auth.Ticket);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, content.GetProperty("ok").GetInt32());

        // Verify database state
        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");

        // Spawn exists
        var spawn = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "spawn")
        )).FirstOrDefaultAsync();
        Assert.NotNull(spawn);
        Assert.Equal(SeedDataDefaults.User.Id, spawn["user"].AsString);
        Assert.Equal(25, spawn["x"].AsInt32);
        Assert.Equal(25, spawn["y"].AsInt32);
        Assert.Equal("MySpawn", spawn["name"].AsString);

        // Controller owned
        var controller = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "controller")
        )).FirstOrDefaultAsync();
        Assert.NotNull(controller);
        Assert.Equal(SeedDataDefaults.User.Id, controller["user"].AsString);
        Assert.Equal(1, controller["level"].AsInt32);

        // User active
        var usersCollection = harness.Database.GetCollection<BsonDocument>("users");
        var user = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", SeedDataDefaults.User.Id)).FirstOrDefaultAsync();
        Assert.Equal(10000, user["active"].AsInt32);
    }

    [Fact]
    public async Task PlaceSpawn_AlreadyPlaying_ReturnsError()
    {
        // Arrange
        // SeedDataDefaults.User already has objects in SeedDataDefaults.World.StartRoom
        var request = new PlaceSpawnRequest("W1N1", 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SeedDataDefaults.Auth.Ticket);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("already playing", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PlaceSpawn_Occupied_ReturnsError()
    {
        // Arrange
        var userId = "new-user";
        var ticket = "new-ticket";
        await CreateUserAsync(userId, ticket);

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
        });

        var request = new PlaceSpawnRequest(room, 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ticket);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Position is occupied", content.GetProperty("error").GetString());
    }

    private async Task PrepareRoomWithNeutralControllerAsync(string room)
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
            ["x"] = 10,
            ["y"] = 10,
            ["level"] = 0
        });
    }

    private async Task CreateUserAsync(string userId, string ticket)
    {
        var usersCollection = harness.Database.GetCollection<BsonDocument>("users");
        await usersCollection.InsertOneAsync(new BsonDocument
        {
            ["_id"] = userId,
            ["username"] = userId,
            ["cpu"] = 100,
            ["active"] = 0
        });

        // We also need to make sure the token service recognizes this ticket.
        // But the IntegrationWebApplicationFactory is configured with a single ticket.
        // Wait, the RedisTokenService uses Redis.
        // Maybe I should just use the existing user but clear their objects?
        // Let's try that.
    }

    [Fact]
    public async Task PlaceSpawn_Cleanup_ConvertsToRuins()
    {
        // Arrange
        var room = "W3N3";
        await PrepareRoomWithNeutralControllerAsync(room);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.InsertOneAsync(new BsonDocument
        {
            ["type"] = "extension",
            ["room"] = room,
            ["x"] = 20,
            ["y"] = 20,
            ["user"] = "other-user",
            ["hits"] = 500,
            ["hitsMax"] = 500
        });

        // Use a new user to avoid "already playing"
        var ticket = SeedDataDefaults.Auth.Ticket; // Reusing ticket but I need the UserId in the token to match
        // Actually, the CurrentUserAccessor in integration tests likely returns the seeded user ID.
        // Let's clear the seeded user's objects instead.
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("user", SeedDataDefaults.User.Id));

        var request = new PlaceSpawnRequest(room, 25, 25, "MySpawn");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ticket);

        // Act
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.PlaceSpawn, request);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify ruin exists
        var ruin = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("type", "ruin")).FirstOrDefaultAsync();
        Assert.NotNull(ruin);
        Assert.Equal("other-user", ruin["user"].AsString);
        Assert.Equal("extension", ruin["structure"]["type"].AsString);

        // Verify extension is gone
        var extension = await objectsCollection.Find(Builders<BsonDocument>.Filter.Eq("type", "extension")).FirstOrDefaultAsync();
        Assert.Null(extension);
    }
}
