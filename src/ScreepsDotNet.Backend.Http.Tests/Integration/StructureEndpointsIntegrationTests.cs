namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class StructureEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();
    private ObjectId _structureId;

    public async Task InitializeAsync()
    {
        await harness.ResetStateAsync();
        var collection = harness.Database.GetCollection<RoomObjectDocument>("rooms.objects");
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Room = SeedDataDefaults.World.StartRoom,
            UserId = SeedDataDefaults.User.Id,
            Type = "extension"
        };
        await collection.InsertOneAsync(document);
        _structureId = document.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetNotifyWhenAttacked_TogglesFlag()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.SetNotifyWhenAttacked, new
        {
            _id = _structureId.ToString(),
            enabled = true
        });

        response.EnsureSuccessStatusCode();

        var collection = harness.Database.GetCollection<RoomObjectDocument>("rooms.objects");
        var updated = await collection.Find(doc => doc.Id == _structureId).FirstOrDefaultAsync();
        Assert.True(updated.NotifyWhenAttacked);
    }

    [Fact]
    public async Task SetNotifyWhenAttacked_InvalidStructure_ReturnsBadRequest()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.SetNotifyWhenAttacked, new
        {
            _id = ObjectId.GenerateNewId().ToString(),
            enabled = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("token").GetString()!;
    }
}
