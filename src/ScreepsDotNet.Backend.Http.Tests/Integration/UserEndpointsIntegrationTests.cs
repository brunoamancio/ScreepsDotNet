namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
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
    private const string UserMessagesCollectionName = "users.messages";
    private const string UserNotificationsCollectionName = "users.notifications";

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
        var remainingRooms = await roomsCollection.CountDocumentsAsync(room => room.UserId == SeedDataDefaults.User.Id);
        Assert.Equal(0, remainingRooms);

        var usersCollection = harness.Database.GetCollection<UserDocument>(UsersCollectionName);
        var user = await usersCollection.Find(u => u.Id == SeedDataDefaults.User.Id)
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
        Assert.Equal(SeedDataDefaults.Money.Description, first.GetProperty("description").GetString());
        Assert.Equal(SeedDataDefaults.Money.Type, first.GetProperty("type").GetString());
    }

    [Fact]
    public async Task UserConsole_Post_PersistsEntry()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Console);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        var expression = SeedDataDefaults.Console.Expression;
        request.Content = JsonContent.Create(new { expression });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var consoleCollection = harness.Database.GetCollection<UserConsoleEntryDocument>(UserConsoleCollectionName);
        var entry = await consoleCollection.Find(doc => doc.UserId == SeedDataDefaults.User.Id)
                                           .SortByDescending(doc => doc.CreatedAt)
                                           .FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(expression, entry!.Expression);
    }

    [Fact]
    public async Task UserMemorySegment_WithToken_ReturnsSeededValue()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.User.MemorySegment}?segment={SeedDataDefaults.Memory.SegmentId}");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(SeedDataDefaults.Memory.SegmentValue, payload.RootElement.GetProperty(UserResponseFields.Data).GetString());
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
            segment = SeedDataDefaults.Memory.SegmentId,
            data = updatedValue
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var memoryCollection = harness.Database.GetCollection<UserMemoryDocument>(UserMemoryCollectionName);
        var document = await memoryCollection.Find(doc => doc.UserId == SeedDataDefaults.User.Id)
                                             .FirstOrDefaultAsync();
        Assert.NotNull(document);
        var key = SeedDataDefaults.Memory.SegmentId.ToString(CultureInfo.InvariantCulture);
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
            ticket = SeedDataDefaults.Auth.Ticket,
            useNativeAuth = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserMessages_List_ReturnsSeededMessage()
    {
        await harness.ResetStateAsync();
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.User.Messages.List}?respondent={SeedDataDefaults.Messaging.RespondentId}");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var messages = payload.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Single(messages);
        var inbound = messages[0];
        Assert.Equal(SeedDataDefaults.Messaging.SampleText, inbound.GetProperty("text").GetString());
        Assert.Equal("in", inbound.GetProperty("type").GetString());
        Assert.True(inbound.GetProperty("unread").GetBoolean());
    }

    [Fact]
    public async Task UserMessages_Index_ReturnsRespondentSummary()
    {
        await harness.ResetStateAsync();
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Messages.Index);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.NotEmpty(messages);
        var summary = messages[0];
        Assert.Equal(SeedDataDefaults.Messaging.RespondentId, summary.GetProperty("_id").GetString());
        var users = root.GetProperty("users");
        Assert.True(users.TryGetProperty(SeedDataDefaults.Messaging.RespondentId, out var userInfo));
        Assert.Equal(SeedDataDefaults.Messaging.RespondentUsername, userInfo.GetProperty("username").GetString());
    }

    [Fact]
    public async Task UserMessages_Send_CreatesDocumentsAndNotifications()
    {
        await harness.ResetStateAsync();
        var token = await AuthenticateAsync();
        var text = "Integration direct message";
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Messages.Send);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            respondent = SeedDataDefaults.Messaging.RespondentId,
            text
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var messagesCollection = harness.Database.GetCollection<UserMessageDocument>(UserMessagesCollectionName);
        var documents = await messagesCollection.Find(doc => doc.Text == text)
                                                .ToListAsync();
        Assert.Equal(2, documents.Count);
        var outbound = Assert.Single(documents, doc => doc.UserId == SeedDataDefaults.User.Id);
        Assert.Equal("out", outbound.Type);
        var inbound = Assert.Single(documents, doc => doc.UserId == SeedDataDefaults.Messaging.RespondentId);
        Assert.Equal("in", inbound.Type);
        Assert.Equal(outbound.Id, inbound.OutMessageId);

        var notifications = harness.Database.GetCollection<UserNotificationDocument>(UserNotificationsCollectionName);
        var notification = await notifications.Find(doc => doc.UserId == SeedDataDefaults.Messaging.RespondentId)
                                              .FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal("msg", notification!.Type);
        Assert.True(notification.Count >= 1);
    }

    [Fact]
    public async Task UserMessages_MarkRead_ClearsUnreadState()
    {
        await harness.ResetStateAsync();
        var messagesCollection = harness.Database.GetCollection<UserMessageDocument>(UserMessagesCollectionName);
        var inbound = await messagesCollection.Find(doc => doc.UserId == SeedDataDefaults.User.Id && doc.Type == "in")
                                              .FirstOrDefaultAsync();
        Assert.NotNull(inbound);
        Assert.True(inbound!.Unread);

        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Messages.MarkRead)
        {
            Content = JsonContent.Create(new
            {
                id = inbound.Id.ToString()
            })
        };
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var updatedInbound = await messagesCollection.Find(doc => doc.Id == inbound.Id)
                                                     .FirstOrDefaultAsync();
        Assert.NotNull(updatedInbound);
        Assert.False(updatedInbound!.Unread);

        if (inbound.OutMessageId != default)
        {
            var counterpart = await messagesCollection.Find(doc => doc.Id == inbound.OutMessageId)
                                                      .FirstOrDefaultAsync();
            Assert.NotNull(counterpart);
            Assert.False(counterpart!.Unread);
        }
    }

    [Fact]
    public async Task UserMessages_UnreadCount_ReturnsCurrentValue()
    {
        await harness.ResetStateAsync();
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Messages.UnreadCount);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, payload.RootElement.GetProperty("count").GetInt32());
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = SeedDataDefaults.Auth.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
