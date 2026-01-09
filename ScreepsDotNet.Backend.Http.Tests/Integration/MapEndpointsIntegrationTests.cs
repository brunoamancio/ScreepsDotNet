namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class MapEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Generate_CreatesRoomAndObjects()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var payload = new
        {
            room = "W30N30",
            sources = 2,
            terrain = "plain",
            keeperLairs = true,
            mineralType = "Z",
            overwrite = true
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Map.Generate, payload);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("W30N30", content.GetProperty("room").GetString());
        Assert.True(content.GetProperty("objects").GetInt32() > 0);

        var rooms = harness.Database.GetCollection<BsonDocument>("rooms");
        var roomDoc = await rooms.Find(doc => doc["_id"] == "W30N30").FirstOrDefaultAsync();
        Assert.NotNull(roomDoc);
        Assert.Equal("normal", roomDoc["status"].AsString);
    }

    [Fact]
    public async Task OpenAndCloseRoom_UpdateStatus()
    {
        await SeedRoomAsync("W31N31", status: "closed");

        var token = await LoginAsync();
        SetAuth(token);

        await _client.PostAsJsonAsync(ApiRoutes.Game.Map.Open, new { room = "W31N31" });
        var status = await GetRoomStatusAsync("W31N31");
        Assert.Equal("normal", status);

        await _client.PostAsJsonAsync(ApiRoutes.Game.Map.Close, new { room = "W31N31" });
        status = await GetRoomStatusAsync("W31N31");
        Assert.Equal("closed", status);
    }

    [Fact]
    public async Task OpenRoom_WithShardPrefix_NormalizesRoom()
    {
        await SeedRoomAsync("W41N41", status: "closed");

        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Map.Open, new { room = "shard7/W41N41" });
        response.EnsureSuccessStatusCode();

        var status = await GetRoomStatusAsync("W41N41");
        Assert.Equal("normal", status);
    }

    [Fact]
    public async Task RemoveRoom_DeletesDocuments()
    {
        await SeedRoomAsync("W32N32");
        var objects = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        await objects.InsertOneAsync(new BsonDocument { ["room"] = "W32N32", ["type"] = "source" });

        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Map.Remove, new { room = "W32N32", purgeObjects = true });
        response.EnsureSuccessStatusCode();

        var rooms = harness.Database.GetCollection<BsonDocument>("rooms");
        Assert.False(await rooms.Find(doc => doc["_id"] == "W32N32").AnyAsync());
        Assert.False(await objects.Find(doc => doc["room"] == "W32N32").AnyAsync());
    }

    [Fact]
    public async Task AssetsUpdate_Succeeds()
    {
        await SeedRoomAsync("W33N33");

        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Map.AssetsUpdate, new { room = "W33N33", full = true });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AssetsUpdate_WithShardProperty_NormalizesInput()
    {
        await SeedRoomAsync("W42N42");

        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Map.AssetsUpdate, new { room = "w42n42", shard = "shard5", full = false });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TerrainRefresh_UpdatesType()
    {
        await SeedRoomAsync("W34N34");
        var terrain = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        await terrain.UpdateOneAsync(doc => doc["room"] == "W34N34",
                                     Builders<BsonDocument>.Update.Set("type", "stale"));

        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsync(ApiRoutes.Game.Map.TerrainRefresh, null);
        response.EnsureSuccessStatusCode();

        var updated = await terrain.Find(doc => doc["room"] == "W34N34").FirstOrDefaultAsync();
        Assert.Equal("terrain", updated["type"].AsString);
    }

    [Fact]
    public async Task Generate_InvalidTerrain_ReturnsError()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var payload = new { room = "W35N35", terrain = "unknown" };
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.Map.Generate, payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new { ticket = SeedDataDefaults.Auth.Ticket });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token)
    {
        if (_client.DefaultRequestHeaders.Contains("X-Token"))
            _client.DefaultRequestHeaders.Remove("X-Token");
        _client.DefaultRequestHeaders.Add("X-Token", token);
    }

    private async Task SeedRoomAsync(string roomName, string status = "normal")
    {
        var rooms = harness.Database.GetCollection<BsonDocument>("rooms");
        await rooms.InsertOneAsync(new BsonDocument { ["_id"] = roomName, ["status"] = status });

        var terrain = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        await terrain.InsertOneAsync(new BsonDocument { ["room"] = roomName, ["type"] = "terrain", ["terrain"] = new string('0', 2500) });
    }

    private async Task<string> GetRoomStatusAsync(string roomName)
    {
        var rooms = harness.Database.GetCollection<BsonDocument>("rooms");
        var doc = await rooms.Find(r => r["_id"] == roomName).FirstOrDefaultAsync();
        return doc["status"].AsString;
    }
}
