namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class ObjectNameEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());
    private const string RoomObjectsCollection = "rooms.objects";

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GenerateUniqueSpawnName_ReturnsNextAvailable()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.ObjectNames.GenerateUnique)
        {
            Content = JsonContent.Create(new { type = "spawn" })
        };
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var generated = payload.RootElement.GetProperty("name").GetString();
        Assert.StartsWith("Spawn", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckUniqueSpawnName_DetectsConflict()
    {
        var token = await AuthenticateAsync();
        var objects = harness.Database.GetCollection<RoomObjectDocument>(RoomObjectsCollection);
        var existingName = "Spawn7";
        await objects.InsertOneAsync(new RoomObjectDocument
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            UserId = SeedDataDefaults.User.Id,
            Type = "spawn",
            Name = existingName,
            Room = SeedDataDefaults.World.StartRoom
        }, cancellationToken: TestContext.Current.CancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.ObjectNames.CheckUnique)
        {
            Content = JsonContent.Create(new { type = "spawn", name = existingName })
        };
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = SeedDataDefaults.Auth.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
