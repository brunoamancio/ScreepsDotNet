namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using StackExchange.Redis;

[Collection(IntegrationTestSuiteDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class SystemEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Status_ReturnsPauseState()
    {
        await SetRedisValue(SystemControlConstants.MainLoopPausedKey, "1");
        await SetRedisValue(SystemControlConstants.MainLoopMinimumDurationKey, "900");

        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.GetAsync(ApiRoutes.Game.System.Status);
        response.EnsureSuccessStatusCode();
        var content = await ReadJsonAsync(response);
        Assert.True(content.GetProperty("paused").GetBoolean());
        Assert.Equal(900, content.GetProperty("tickDuration").GetInt32());
    }

    [Fact]
    public async Task PauseAndResume_ToggleRedisFlag()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var pauseResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.System.Pause));
        pauseResponse.EnsureSuccessStatusCode();
        Assert.Equal("1", await GetRedisValue(SystemControlConstants.MainLoopPausedKey));

        var resumeResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.System.Resume));
        resumeResponse.EnsureSuccessStatusCode();
        Assert.Equal("0", await GetRedisValue(SystemControlConstants.MainLoopPausedKey));
    }

    [Fact]
    public async Task TickDuration_SetAndGet()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var setPayload = JsonContent.Create(new { duration = 750 });
        var setResponse = await _client.PostAsync(ApiRoutes.Game.System.TickSet, setPayload);
        setResponse.EnsureSuccessStatusCode();

        Assert.Equal("750", await GetRedisValue(SystemControlConstants.MainLoopMinimumDurationKey));

        var getResponse = await _client.GetAsync(ApiRoutes.Game.System.Tick);
        getResponse.EnsureSuccessStatusCode();
        var content = await ReadJsonAsync(getResponse);
        Assert.Equal(750, content.GetProperty("duration").GetInt32());
    }

    [Fact]
    public async Task Message_PublishesChannel()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var subscriber = await ConnectionMultiplexer.ConnectAsync(harness.RedisConnectionString);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new RedisChannel(SystemControlConstants.ServerMessageChannel, RedisChannel.PatternMode.Literal);
        await subscriber.GetSubscriber().SubscribeAsync(channel, (_, value) => tcs.TrySetResult(value!));

        const string payload = "integration broadcast";
        var messageResponse = await _client.PostAsJsonAsync(ApiRoutes.Game.System.Message, new { message = payload });
        messageResponse.EnsureSuccessStatusCode();

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(payload, received);
        await subscriber.DisposeAsync();
    }

    [Fact]
    public async Task Reset_RequiresConfirmation()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.System.Reset, new { confirm = "nope" });
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_ReplaysSeedData_WhenConfirmed()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var users = harness.Database.GetCollection<BsonDocument>("users");
        var filter = Builders<BsonDocument>.Filter.Eq("_id", SeedDataDefaults.User.Id);
        await users.UpdateOneAsync(filter,
                                   Builders<BsonDocument>.Update.Set("username", "TamperedUser"),
                                   cancellationToken: TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.System.Reset, new { confirm = "RESET" });
        response.EnsureSuccessStatusCode();

        var restored = await users.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(SeedDataDefaults.User.Username, restored["username"].AsString);
    }

    [Fact]
    public async Task StorageStatus_ReturnsAdapterHealth()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var response = await _client.GetAsync(ApiRoutes.Game.System.StorageStatus);
        response.EnsureSuccessStatusCode();
        var content = await ReadJsonAsync(response);
        Assert.True(content.GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task StorageReseed_MirrorsResetSemantics()
    {
        var token = await LoginAsync();
        SetAuth(token);

        var users = harness.Database.GetCollection<BsonDocument>("users");
        var filter = Builders<BsonDocument>.Filter.Eq("_id", SeedDataDefaults.User.Id);
        await users.UpdateOneAsync(filter,
                                   Builders<BsonDocument>.Update.Set("username", "TamperedStorageUser"),
                                   cancellationToken: TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.System.StorageReseed, new { confirm = "RESET" });
        response.EnsureSuccessStatusCode();

        var restored = await users.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(SeedDataDefaults.User.Username, restored["username"].AsString);
    }

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        return content.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token)
    {
        if (_client.DefaultRequestHeaders.Contains("X-Token"))
            _client.DefaultRequestHeaders.Remove("X-Token");
        _client.DefaultRequestHeaders.Add("X-Token", token);
    }

    private async Task<string> GetRedisValue(string key)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(harness.RedisConnectionString);
        var value = await connection.GetDatabase().StringGetAsync(key);
        return value.HasValue ? value.ToString() : string.Empty;
    }

    private async Task SetRedisValue(string key, string value)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(harness.RedisConnectionString);
        await connection.GetDatabase().StringSetAsync(key, value);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await TestHttpClient.ReadAsStringAsync(response);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
