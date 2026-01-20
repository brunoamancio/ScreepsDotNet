namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class PowerCreepEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task List_ReturnsSeededCreeps()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.GetAsync(ApiRoutes.Game.PowerCreeps.List);
        response.EnsureSuccessStatusCode();
        var json = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);

        Assert.Equal(1, json.GetProperty("ok").GetInt32());
        var creeps = json.GetProperty("list").EnumerateArray().ToList();
        Assert.NotEmpty(creeps);
        var active = creeps.FirstOrDefault(item => item.GetProperty("_id").GetString() == SeedDataDefaults.PowerCreeps.ActiveId);
        Assert.NotEqual(JsonValueKind.Undefined, active.ValueKind);
        Assert.Equal(SeedDataDefaults.PowerCreeps.ActiveRoom, active.GetProperty("room").GetString());
        Assert.Equal(SeedDataDefaults.PowerCreeps.ActiveShardName, active.GetProperty("shard").GetString());
    }

    [Fact]
    public async Task Create_AddsPowerCreep()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var payload = new { name = "PowerIntegration", className = SeedDataDefaults.PowerCreeps.ClassName };
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.PowerCreeps.Create, payload);
        response.EnsureSuccessStatusCode();
        var json = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);

        Assert.Equal(1, json.GetProperty("ok").GetInt32());
        var creepId = json.GetProperty("creep").GetProperty("_id").GetString();
        Assert.False(string.IsNullOrEmpty(creepId));

        var doc = await GetPowerCreepDocumentAsync(creepId!);
        Assert.Equal("PowerIntegration", doc["name"].AsString);
    }

    [Fact]
    public async Task Rename_UnspawnedCreep_Succeeds()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var payload = new { id = SeedDataDefaults.PowerCreeps.DormantId, name = "RenamedOperator" };
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.PowerCreeps.Rename, payload);
        response.EnsureSuccessStatusCode();

        var doc = await GetPowerCreepDocumentAsync(SeedDataDefaults.PowerCreeps.DormantId);
        Assert.Equal("RenamedOperator", doc["name"].AsString);
    }

    [Fact]
    public async Task DeleteAndCancelDelete_UpdateDeleteTime()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var deleteResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.PowerCreeps.Delete, new { id = SeedDataDefaults.PowerCreeps.DormantId });
        deleteResponse.EnsureSuccessStatusCode();

        var deletedDoc = await GetPowerCreepDocumentAsync(SeedDataDefaults.PowerCreeps.DormantId);
        Assert.True(deletedDoc.Contains("deleteTime"));

        var cancelResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.PowerCreeps.CancelDelete, new { id = SeedDataDefaults.PowerCreeps.DormantId });
        cancelResponse.EnsureSuccessStatusCode();

        var restoredDoc = await GetPowerCreepDocumentAsync(SeedDataDefaults.PowerCreeps.DormantId);
        Assert.False(restoredDoc.Contains("deleteTime"));
    }

    [Fact]
    public async Task Upgrade_IncreasesLevel()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var payload = new
        {
            id = SeedDataDefaults.PowerCreeps.DormantId,
            powers = new Dictionary<string, int>
            {
                ["1"] = 1,
                ["2"] = 1
            }
        };

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.PowerCreeps.Upgrade, payload);
        response.EnsureSuccessStatusCode();

        var doc = await GetPowerCreepDocumentAsync(SeedDataDefaults.PowerCreeps.DormantId);
        Assert.Equal(2, doc["level"].AsInt32);

        // Powers are stored as ArrayOfDocuments: [{ k: 1, v: { level: 1 } }, ...]
        var powers = doc["powers"].AsBsonArray;
        var power1 = powers.FirstOrDefault(p => p["k"].AsInt32 == 1);
        var power2 = powers.FirstOrDefault(p => p["k"].AsInt32 == 2);
        Assert.NotNull(power1);
        Assert.NotNull(power2);
        Assert.Equal(1, power1["v"]["level"].AsInt32);
        Assert.Equal(1, power2["v"]["level"].AsInt32);
    }

    [Fact]
    public async Task Experimentation_DecrementsCounter()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.PowerCreeps.Experimentation));
        response.EnsureSuccessStatusCode();

        var users = harness.Database.GetCollection<BsonDocument>("users");
        var user = await users.Find(doc => doc["_id"] == SeedDataDefaults.User.Id)
                              .FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, user["powerExperimentations"].ToInt32());
        Assert.True(user["powerExperimentationTime"].ToInt64() > 0);
    }

    [Fact]
    public async Task Delete_WhenSpawned_ReturnsError()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.PowerCreeps.Delete, new { id = SeedDataDefaults.PowerCreeps.ActiveId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new { ticket = SeedDataDefaults.Auth.Ticket });
        response.EnsureSuccessStatusCode();
        var json = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        return json.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token)
    {
        if (_client.DefaultRequestHeaders.Contains("X-Token"))
            _client.DefaultRequestHeaders.Remove("X-Token");
        _client.DefaultRequestHeaders.Add("X-Token", token);
    }

    private async Task<BsonDocument> GetPowerCreepDocumentAsync(string id)
    {
        var collection = harness.Database.GetCollection<BsonDocument>("users.power_creeps");
        return await collection.Find(doc => doc["_id"] == ObjectId.Parse(id)).FirstAsync();
    }
}
