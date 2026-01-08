namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class IntentEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, payload.GetProperty("ok").GetInt32());

        var intents = harness.Database.GetCollection<BsonDocument>("rooms.intents");
        var document = await intents.Find(new BsonDocument("room", room)).FirstOrDefaultAsync();
        Assert.NotNull(document);

        var usersDoc = document["users"].AsBsonDocument;
        var userIntentDoc = usersDoc[SeedDataDefaults.User.Id].AsBsonDocument;
        var objectsManual = userIntentDoc["objectsManual"].AsBsonDocument;
        var intentDoc = objectsManual["object-123"].AsBsonDocument["move"].AsBsonDocument;
        Assert.Equal(3, intentDoc["direction"].AsInt32);
        Assert.Equal("object-123", intentDoc["id"].AsString);
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
        });

        var request = new
        {
            room,
            _id = "controller-id",
            name = "activateSafeMode",
            intent = new { }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Intent.AddObject, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
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

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, payload.GetProperty("ok").GetInt32());

        var collection = harness.Database.GetCollection<BsonDocument>("users.intents");
        var document = await collection.Find(new BsonDocument("user", SeedDataDefaults.User.Id))
                                       .FirstOrDefaultAsync()
                                       ;

        Assert.NotNull(document);
        var intents = document["intents"].AsBsonDocument;
        var notify = intents["notify"].AsBsonArray.Single().AsBsonDocument;
        Assert.Equal("Tick complete", notify["message"].AsString);
        Assert.Equal(10, notify["groupInterval"].AsInt32);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new { ticket = SeedDataDefaults.Auth.Ticket });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private async Task EnsureRoomAsync(string room)
    {
        var rooms = harness.Database.GetCollection<BsonDocument>("rooms");
        await rooms.ReplaceOneAsync(new BsonDocument("_id", room),
                                    new BsonDocument
                                    {
                                        ["_id"] = room,
                                        ["status"] = "normal",
                                        ["openTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000
                                    },
                                    new ReplaceOptions { IsUpsert = true });
    }
}
