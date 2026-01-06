namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class UserEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestHarness _harness;
    private readonly HttpClient _client;
    private const string RoomsObjectsCollectionName = "rooms.objects";
    private const string UsersCollectionName = "users";
    private const string LastRespawnDateField = "lastRespawnDate";

    public UserEndpointsIntegrationTests(IntegrationTestHarness harness)
    {
        _harness = harness;
        _client = harness.Factory.CreateClient();
    }

    public Task InitializeAsync() => _harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Respawn_RemovesRoomsAndUpdatesLastRespawn()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Respawn);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Timestamp, out _));

        var roomsCollection = _harness.Database.GetCollection<BsonDocument>(RoomsObjectsCollectionName);
        var remainingRooms = await roomsCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq(UserResponseFields.User, IntegrationTestValues.UserId));
        Assert.Equal(0, remainingRooms);

        var usersCollection = _harness.Database.GetCollection<BsonDocument>(UsersCollectionName);
        var user = await usersCollection.Find(Builders<BsonDocument>.Filter.Eq(UserResponseFields.Id, IntegrationTestValues.UserId))
                                        .FirstOrDefaultAsync();

        Assert.NotNull(user);
        Assert.True(user.TryGetValue(LastRespawnDateField, out var lastRespawn));
        Assert.True(lastRespawn.ToUniversalTime() >= _harness.InitializedAtUtc.AddMinutes(-1));
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = IntegrationTestValues.AuthTicket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
