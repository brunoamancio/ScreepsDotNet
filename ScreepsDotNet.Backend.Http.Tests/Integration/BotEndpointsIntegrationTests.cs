namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using Xunit.Sdk;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class BotEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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
        var userDoc = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", userId)).FirstOrDefaultAsync();
        Assert.NotNull(userDoc);
        Assert.Equal("alpha", userDoc["bot"].AsString);
        Assert.Equal("alphacommander", userDoc["usernameLower"].AsString);

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnDoc = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "spawn"),
            Builders<BsonDocument>.Filter.Eq("user", userId)))
            .FirstOrDefaultAsync();
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
        var userDoc = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq("username", username)).FirstOrDefaultAsync();
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
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user not found", content.GetProperty("error").GetString());
    }

    private async Task<JsonElement> SpawnBotAsync(string room)
    {
        var payload = new { bot = "alpha", room };
        return await ReadJsonAsync(await _client.PostAsJsonAsync(ApiRoutes.Game.Bot.Spawn, payload));
    }

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("token").GetString()!;
    }

    private void SetAuthHeader(string token)
    {
        if (_client.DefaultRequestHeaders.Contains("X-Token"))
            _client.DefaultRequestHeaders.Remove("X-Token");
        _client.DefaultRequestHeaders.Add("X-Token", token);
    }

    private async Task PrepareNeutralRoomAsync(string room)
    {
        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        await roomsCollection.ReplaceOneAsync(new BsonDocument("_id", room),
                                              new BsonDocument { ["_id"] = room, ["status"] = "normal" },
                                              new ReplaceOptions { IsUpsert = true });

        var terrainCollection = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        await terrainCollection.ReplaceOneAsync(new BsonDocument("room", room),
                                                new BsonDocument { ["room"] = room, ["terrain"] = new string('0', 2500) },
                                                new ReplaceOptions { IsUpsert = true });

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objectsCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("room", room));
        await objectsCollection.InsertOneAsync(new BsonDocument
        {
            ["type"] = "controller",
            ["room"] = room,
            ["x"] = 25,
            ["y"] = 25,
            ["level"] = 0
        });
    }
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new XunitException($"Expected success but received {(int)response.StatusCode} {response.StatusCode}. Payload: {json}");

        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
