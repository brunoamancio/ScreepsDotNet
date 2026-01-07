namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class UserEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();
    private const string RoomsObjectsCollectionName = "rooms.objects";
    private const string UsersCollectionName = "users";
    private const string UserConsoleCollectionName = "users.console";
    private const string UserMemoryCollectionName = "users.memory";

    public Task InitializeAsync() => harness.ResetStateAsync();

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

        var roomsCollection = harness.Database.GetCollection<RoomObjectDocument>(RoomsObjectsCollectionName);
        var remainingRooms = await roomsCollection.CountDocumentsAsync(room => room.UserId == IntegrationTestValues.User.Id);
        Assert.Equal(0, remainingRooms);

        var usersCollection = harness.Database.GetCollection<UserDocument>(UsersCollectionName);
        var user = await usersCollection.Find(u => u.Id == IntegrationTestValues.User.Id)
                                        .FirstOrDefaultAsync();

        Assert.NotNull(user);
        Assert.True(user!.LastRespawnDate >= harness.InitializedAtUtc.AddMinutes(-1));
    }

    [Fact]
    public async Task UserMoneyHistory_WithToken_ReturnsSeededEntry()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.MoneyHistory + "?page=0");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var list = payload.RootElement.GetProperty(UserResponseFields.List).EnumerateArray().ToList();
        Assert.NotEmpty(list);
        var first = list.First();
        Assert.Equal(IntegrationTestValues.Money.Description, first.GetProperty("description").GetString());
        Assert.Equal(IntegrationTestValues.Money.Type, first.GetProperty("type").GetString());
    }

    [Fact]
    public async Task UserConsole_Post_PersistsEntry()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Console);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        var expression = IntegrationTestValues.Console.Expression;
        request.Content = JsonContent.Create(new { expression });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var consoleCollection = harness.Database.GetCollection<UserConsoleEntryDocument>(UserConsoleCollectionName);
        var entry = await consoleCollection.Find(doc => doc.UserId == IntegrationTestValues.User.Id)
                                           .SortByDescending(doc => doc.CreatedAt)
                                           .FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(expression, entry!.Expression);
    }

    [Fact]
    public async Task UserMemorySegment_WithToken_ReturnsSeededValue()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.User.MemorySegment}?segment={IntegrationTestValues.Memory.SegmentId}");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(IntegrationTestValues.Memory.SegmentValue, payload.RootElement.GetProperty(UserResponseFields.Data).GetString());
    }

    [Fact]
    public async Task UserMemorySegment_Post_UpdatesSegment()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.MemorySegment);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        const string updatedValue = "updated-segment";
        request.Content = JsonContent.Create(new
        {
            segment = IntegrationTestValues.Memory.SegmentId,
            data = updatedValue
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var memoryCollection = harness.Database.GetCollection<UserMemoryDocument>(UserMemoryCollectionName);
        var document = await memoryCollection.Find(doc => doc.UserId == IntegrationTestValues.User.Id)
                                             .FirstOrDefaultAsync();
        Assert.NotNull(document);
        var key = IntegrationTestValues.Memory.SegmentId.ToString(CultureInfo.InvariantCulture);
        Assert.True(document!.Segments.TryGetValue(key, out var data));
        Assert.Equal(updatedValue, data);
    }

    [Fact]
    public async Task UserStats_WithInterval_ReturnsPayload()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Stats + "?interval=8");

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stats = payload.RootElement.GetProperty(UserResponseFields.Stats);
        Assert.Equal(8, stats.GetProperty(UserResponseFields.Interval).GetInt32());
        Assert.True(stats.GetProperty(UserResponseFields.ActiveUsers).GetInt32() > 0);
        Assert.True(stats.GetProperty(UserResponseFields.RoomsControlled).GetInt32() > 0);
    }

    [Fact]
    public async Task AuthSteamTicket_InvalidTicket_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = "invalid-ticket",
            useNativeAuth = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthSteamTicket_NativeAuthMismatch_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = IntegrationTestValues.Auth.Ticket,
            useNativeAuth = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = IntegrationTestValues.Auth.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
