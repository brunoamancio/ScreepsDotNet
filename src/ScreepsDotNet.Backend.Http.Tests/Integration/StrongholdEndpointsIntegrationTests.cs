namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class StrongholdEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Templates_ReturnsData()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync(ApiRoutes.Game.Stronghold.Templates);
        response.EnsureSuccessStatusCode();

        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
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
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
        Assert.Equal(room, content.GetProperty("room").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("shard").ValueKind);
        Assert.Equal("bunker1", content.GetProperty("template").GetString());
        Assert.False(string.IsNullOrWhiteSpace(content.GetProperty("strongholdId").GetString()));

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var core = await objectsCollection.Find(Builders<BsonDocument>.Filter.And(
                                                    Builders<BsonDocument>.Filter.Eq("room", room),
                                                    Builders<BsonDocument>.Filter.Eq("type", "invaderCore")))
                                          .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(core);
        Assert.Equal("bunker1", core["templateName"].AsString);
    }

    [Fact]
    public async Task Spawn_WithShard_PersistsShardMetadata()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = "W42N42";
        const string shard = "shard9";
        await PrepareRoomAsync(room, shard);

        var payload = new
        {
            room,
            shard,
            template = "bunker2",
            x = 18,
            y = 19
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Spawn, payload);
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(shard, content.GetProperty("shard").GetString());

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("shard", shard),
            Builders<BsonDocument>.Filter.Eq("type", "invaderCore"));
        var core = await objectsCollection.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(core);
        Assert.Equal(shard, core["shard"].AsString);
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
        var coreBefore = await objectsCollection.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(coreBefore);
        var previousNextExpand = coreBefore!["nextExpandTime"].AsInt32;

        var expandPayload = new { room };
        var expandResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Expand, expandPayload);
        expandResponse.EnsureSuccessStatusCode();

        var coreAfter = await objectsCollection.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
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
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("stronghold not found", content.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Expand_WithShardTargetsMatchingCore()
    {
        var token = await LoginAsync();
        SetAuthHeader(token);

        var room = SeedDataDefaults.Strongholds.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;

        var objectsCollection = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var shardFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("shard", shard),
            Builders<BsonDocument>.Filter.Eq("type", "invaderCore"));
        var seededCore = await objectsCollection.Find(shardFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(seededCore);
        var originalNextExpand = seededCore["nextExpandTime"].AsInt32;

        var nonShardFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", SeedDataDefaults.World.SecondaryRoom),
            Builders<BsonDocument>.Filter.Eq("type", "invaderCore"));
        var nonShardCoreBefore = await objectsCollection.Find(nonShardFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Stronghold.Expand, new { room, shard });
        response.EnsureSuccessStatusCode();

        var updatedCore = await objectsCollection.Find(shardFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(updatedCore);
        Assert.NotEqual(originalNextExpand, updatedCore["nextExpandTime"].AsInt32);

        if (nonShardCoreBefore is not null && nonShardCoreBefore.TryGetValue("nextExpandTime", out var beforeValue)) {
            var nonShardCoreAfter = await objectsCollection.Find(nonShardFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            if (nonShardCoreAfter is not null && nonShardCoreAfter.TryGetValue("nextExpandTime", out var afterValue))
                Assert.Equal(beforeValue, afterValue);
        }
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

    private async Task PrepareRoomAsync(string room, string? shard = null)
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
            ["x"] = 1,
            ["y"] = 1
        };
        if (shard is not null)
            controllerDoc["shard"] = shard;
        await objectsCollection.InsertOneAsync(controllerDoc, cancellationToken: TestContext.Current.CancellationToken);
    }
}
