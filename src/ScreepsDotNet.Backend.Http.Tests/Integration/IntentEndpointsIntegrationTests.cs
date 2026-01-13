namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System;
using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class IntentEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AddObjectIntent_Success()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        const string room = "W1N1";
        await EnsureRoomAsync(room);

        var request = new
        {
            room,
            _id = "object-123",
            name = "move",
            intent = new { id = "object-123", direction = 3 }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Intent.AddObject, request);
        response.EnsureSuccessStatusCode();

        var payload = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, payload.GetProperty("ok").GetInt32());

        var intents = harness.Database.GetCollection<BsonDocument>("rooms.intents");
        var document = await intents.Find(new BsonDocument("room", room)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(document);

        var usersDoc = document["users"].AsBsonDocument;
        var userIntentDoc = usersDoc[SeedDataDefaults.User.Id].AsBsonDocument;
        var objectsManual = userIntentDoc["objectsManual"].AsBsonDocument;
        var intentDoc = objectsManual["object-123"].AsBsonDocument["move"].AsBsonDocument;
        Assert.Equal(3, intentDoc["direction"].AsInt32);
        Assert.Equal("object-123", intentDoc["id"].AsString);
    }

    [Fact]
    public async Task AddObjectIntent_WithShard_PersistsShardScopedDocument()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var room = SeedDataDefaults.Intents.SecondaryShardRoom;
        var shard = SeedDataDefaults.World.SecondaryShardName;
        await EnsureRoomAsync(room, shard);

        var request = new
        {
            room,
            shard,
            _id = SeedDataDefaults.Intents.SecondaryShardObjectId,
            name = "move",
            intent = new { id = SeedDataDefaults.Intents.SecondaryShardObjectId, direction = 6 }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Intent.AddObject, request);
        response.EnsureSuccessStatusCode();

        var intents = harness.Database.GetCollection<BsonDocument>("rooms.intents");
        var shardFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("shard", shard));
        var shardDocument = await intents.Find(shardFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(shardDocument);

        var usersDoc = shardDocument["users"].AsBsonDocument;
        var userIntentDoc = usersDoc[SeedDataDefaults.User.Id].AsBsonDocument;
        var objectsManual = userIntentDoc["objectsManual"].AsBsonDocument;
        var intentDoc = objectsManual[SeedDataDefaults.Intents.SecondaryShardObjectId].AsBsonDocument["move"].AsBsonDocument;
        Assert.Equal(6, intentDoc["direction"].AsInt32);

        var primaryFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Exists("shard", false),
                Builders<BsonDocument>.Filter.Eq("shard", BsonNull.Value)));
        var primaryDocument = await intents.Find(primaryFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(primaryDocument);
    }

    [Fact]
    public async Task AddObjectIntent_SafeModeActive_ReturnsError()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        const string room = "W2N2";
        await EnsureRoomAsync(room);

        var objects = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objects.InsertOneAsync(new BsonDocument
        {
            ["type"] = "controller",
            ["room"] = room,
            ["user"] = SeedDataDefaults.User.Id,
            ["safeMode"] = SeedDataDefaults.World.GameTime + 100
        }, cancellationToken: TestContext.Current.CancellationToken);

        var request = new
        {
            room,
            _id = "controller-id",
            name = "activateSafeMode",
            intent = new { }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Intent.AddObject, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal("safe mode active already", payload.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AddGlobalIntent_Success()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var request = new
        {
            name = "notify",
            intent = new { message = "Tick complete", groupInterval = 10 }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Intent.AddGlobal, request);
        response.EnsureSuccessStatusCode();

        var payload = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, payload.GetProperty("ok").GetInt32());

        var collection = harness.Database.GetCollection<BsonDocument>("users.intents");
        var document = await collection.Find(new BsonDocument("user", SeedDataDefaults.User.Id))
                                       .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                                       .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(document);
        var intents = document["intents"].AsBsonDocument;
        var notify = intents["notify"].AsBsonArray.Single().AsBsonDocument;
        Assert.Equal("Tick complete", notify["message"].AsString);
        Assert.Equal(10, notify["groupInterval"].AsInt32);
    }

    [Fact]
    public async Task AddGlobalIntent_CustomIntent_UsesManifestSchema()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var request = new
        {
            name = "shieldRoom",
            intent = new { roomName = "W9N9", duration = 25 }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Intent.AddGlobal, request);
        response.EnsureSuccessStatusCode();

        var payload = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, payload.GetProperty("ok").GetInt32());

        var collection = harness.Database.GetCollection<BsonDocument>("users.intents");
        var document = await collection.Find(new BsonDocument("user", SeedDataDefaults.User.Id))
                                       .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                                       .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(document);
        var intents = document["intents"].AsBsonDocument;
        var custom = intents["shieldRoom"].AsBsonArray.Single().AsBsonDocument;
        Assert.Equal("W9N9", custom["roomName"].AsString);
        Assert.Equal(25, custom["duration"].AsInt32);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new { ticket = SeedDataDefaults.Auth.Ticket });
        response.EnsureSuccessStatusCode();
        var json = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        return json.GetProperty("token").GetString()!;
    }

    private async Task EnsureRoomAsync(string room, string? shard = null)
    {
        var rooms = harness.Database.GetCollection<BsonDocument>("rooms");
        var roomDoc = new BsonDocument
        {
            ["_id"] = room,
            ["status"] = "normal",
            ["openTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000
        };
        if (shard is not null)
            roomDoc["shard"] = shard;
        await rooms.ReplaceOneAsync(new BsonDocument("_id", room),
                                    roomDoc,
                                    new ReplaceOptions { IsUpsert = true }, cancellationToken: TestContext.Current.CancellationToken);
    }
}
