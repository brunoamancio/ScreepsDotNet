namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class StrongholdEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Templates_ReturnsData()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync(ApiRoutes.Game.Stronghold.Templates);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        var templates = content.GetProperty("templates");
        Assert.True(templates.GetArrayLength() > 0);
        var firstTemplate = templates[0];
        Assert.True(firstTemplate.GetProperty("structures").GetArrayLength() > 0);
        var depositTypes = content.GetProperty("depositTypes");
        Assert.True(depositTypes.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Spawn_CreatesStronghold()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = "W40N40";
        await PrepareRoomAsync(room);

        var payload = new
        {
            room,
            template = "bunker1",
            x = 15,
            y = 15
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Spawn, payload);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        Assert.Equal(room, content.GetProperty("room").GetString());
        Assert.Equal("bunker1", content.GetProperty("template").GetString());
        Assert.False(string.IsNullOrWhiteSpace(content.GetProperty("strongholdId").GetString()));

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var core = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
                                                    Builders<BsonDocument>.Filter.Eq("room", room),
                                                    Builders<BsonDocument>.Filter.Eq("type", "invaderCore")))
                                          .FirstOrDefaultAsync();
        Assert.NotNull(core);
        Assert.Equal("bunker1", core["templateName"].AsString);
    }

    [Fact]
    public async Task Expand_StrongholdUpdatesNextExpandTime()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = "W41N41";
        await PrepareRoomAsync(room);

        var spawnPayload = new { room, template = "bunker2", x = 20, y = 20 };
        var spawnResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Spawn, spawnPayload);
        spawnResponse.EnsureSuccessStatusCode();

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", "invaderCore"));
        var coreBefore = await objectsCollection.Find(filter).FirstOrDefaultAsync();
        Assert.NotNull(coreBefore);
        var previousNextExpand = coreBefore!["nextExpandTime"].AsInt32;

        var expandPayload = new { room };
        var expandResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Expand, expandPayload);
        expandResponse.EnsureSuccessStatusCode();

        var coreAfter = await objectsCollection.Find(filter).FirstOrDefaultAsync();
        Assert.NotNull(coreAfter);
        Assert.NotEqual(previousNextExpand, coreAfter!["nextExpandTime"].AsInt32);
    }

    [Fact]
    public async Task Expand_MissingCoreReturnsError()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var payload = new { room = "W50N50" };
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Expand, payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stronghold not found", content.GetProperty("error").GetString());
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

    private async Task PrepareRoomAsync(string room)
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
            ["x"] = 1,
            ["y"] = 1
        });
    }
}
