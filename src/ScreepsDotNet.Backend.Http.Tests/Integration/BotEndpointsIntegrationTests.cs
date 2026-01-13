namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using Xunit.Sdk;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class BotEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ListBots_ReturnsDefinitions()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var content = await ReadJsonAsync(await _client.GetAsync(ApiRoutes.Game.Bot.List));
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        var bots = content.GetProperty("bots");
        Assert.True(bots.GetArrayLength() >= 1);
        Assert.Contains(bots.EnumerateArray(), bot => bot.GetProperty("name").GetString() == "alpha");
    }

    [Fact]
    public async Task SpawnBot_Success()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = "W9N9";
        await PrepareNeutralRoomAsync(room);

        var payload = new
        {
            bot = "alpha",
            room,
            username = "AlphaCommander",
            cpu = 120,
            gcl = 2,
            x = 20,
            y = 20
        };

        var content = await ReadJsonAsync(await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Spawn, payload));
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        var userId = content.GetProperty("userId").GetString();
        var username = content.GetProperty("username").GetString();
        Assert.False(string.IsNullOrEmpty(userId));
        Assert.Equal("AlphaCommander", username);

        var usersCollection = harness.Database.GetCollection<BsonDocument>("users");
        var userDoc = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", userId)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(userDoc);
        Assert.Equal("alpha", userDoc["bot"].AsString);
        Assert.Equal("alphacommander", userDoc["usernameLower"].AsString);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnDoc = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "spawn"),
            Builders<BsonDocument>.Filter.Eq("user", userId)))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(spawnDoc);
    }

    [Fact]
    public async Task ReloadBot_ReturnsCount()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = "W8N8";
        await PrepareNeutralRoomAsync(room);
        await SpawnBotAsync(room);

        var payload = new { bot = "alpha" };
        var content = await ReadJsonAsync(await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Reload, payload));
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        Assert.True(content.GetProperty("usersReloaded").GetInt32() >= 1);
    }

    [Fact]
    public async Task RemoveBot_DeletesUser()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = "W7N7";
        await PrepareNeutralRoomAsync(room);
        var spawnResponse = await SpawnBotAsync(room);
        var username = spawnResponse.GetProperty("username").GetString()!;

        var payload = new { username };
        var content = await ReadJsonAsync(await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Remove, payload));
        Assert.Equal(1, content.GetProperty("ok").GetInt32());

        var usersCollection = harness.Database.GetCollection<BsonDocument>("users");
        var userDoc = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq("username", username)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(userDoc);
    }

    [Fact]
    public async Task RemoveBot_NotFoundReturnsError()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var payload = new { username = "missing-bot" };
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Remove, payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("user not found", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SpawnBot_WithShard_PersistsShardMetadata()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = SeedDataDefaults.Bots.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;
        await PrepareNeutralRoomAsync(room, shard);

        var payload = new
        {
            bot = "alpha",
            room,
            shard,
            username = "ShardCommander"
        };

        var content = await ReadJsonAsync(await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Spawn, payload));
        Assert.Equal(shard, content.GetProperty("shard").GetString());

        var userId = content.GetProperty("userId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("shard", shard),
            Builders<BsonDocument>.Filter.Eq("type", "spawn"),
            Builders<BsonDocument>.Filter.Eq("user", userId));
        var spawnDocument = await objectsCollection.Find(spawnFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(spawnDocument);

        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        var roomDocument = await roomsCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", room)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(roomDocument);
        Assert.Equal(shard, roomDocument["shard"].AsString);
    }

    [Fact]
    public async Task RemoveBot_ShardsRemainIsolated()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        const string primaryRoom = "W70N70";
        await PrepareNeutralRoomAsync(primaryRoom);
        var primarySpawn = await SpawnBotAsync(primaryRoom, username: "PrimaryCommander");
        var primaryUsername = primarySpawn.GetProperty("username").GetString()!;

        var shardRoom = SeedDataDefaults.Bots.SecondaryShardRoom;
        var shardName = SeedDataDefaults.World.SecondaryShardName;
        await PrepareNeutralRoomAsync(shardRoom, shardName);
        var shardSpawn = await SpawnBotAsync(shardRoom, shardName, "ShardCommander");
        var shardUsername = shardSpawn.GetProperty("username").GetString()!;

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Remove, new { username = shardUsername });
        response.EnsureSuccessStatusCode();

        var users = harness.Database.GetCollection<BsonDocument>("users");
        Assert.True(await users.Find(Builders<BsonDocument>.Filter.Eq("username", primaryUsername)).AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await users.Find(Builders<BsonDocument>.Filter.Eq("username", shardUsername)).AnyAsync(TestContext.Current.CancellationToken));

        var objects = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        Assert.True(await objects.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", primaryRoom),
            Builders<BsonDocument>.Filter.Eq("type", "spawn"))).AnyAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> SpawnBotAsync(string room, string? shard = null, string? username = null)
    {
        var payload = new
        {
            bot = "alpha",
            room,
            shard,
            username = username ?? "AutoCommander"
        };

        return await ReadJsonAsync(await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Spawn, payload));
    }

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        return content.GetProperty("token").GetString()!;
    }

    private void SetAuthHeader(string token)
    {
        if (_client.DefaultRequestHeaders.Contains("X-Token"))
            _client.DefaultRequestHeaders.Remove("X-Token");
        _client.DefaultRequestHeaders.Add("X-Token", token);
    }

    private async Task PrepareNeutralRoomAsync(string room, string? shard = null)
    {
        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        var roomDoc = new BsonDocument { ["_id"] = room, ["status"] = "normal" };
        if (shard is not null)
            roomDoc["shard"] = shard;
        await roomsCollection.ReplaceOneAsync(new BsonDocument("_id", room),
                                              roomDoc,
                                              new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);

        var terrainCollection = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        var terrainDoc = new BsonDocument { ["room"] = room, ["terrain"] = new string('0', 2500) };
        if (shard is not null)
            terrainDoc["shard"] = shard;
        await terrainCollection.ReplaceOneAsync(new BsonDocument("room", room),
                                                terrainDoc,
                                                new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("room", room), cancellationToken: TestContext.Current.CancellationToken);
        var controllerDoc = new BsonDocument
        {
            ["type"] = "controller",
            ["room"] = room,
            ["x"] = 25,
            ["y"] = 25,
            ["level"] = 0
        };
        if (shard is not null)
            controllerDoc["shard"] = shard;
        await objectsCollection.InsertOneAsync(controllerDoc, cancellationToken: TestContext.Current.CancellationToken);
    }
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await TestHttpClient.ReadAsStringAsync(response);
        if (!response.IsSuccessStatusCode)
            throw new XunitException($"Expected success but received {(int)response.StatusCode} {response.StatusCode}. Payload: {json}");

        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
