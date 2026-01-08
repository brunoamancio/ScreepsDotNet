namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net.Http.Json;
using System.Text.Json;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using StackExchange.Redis;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class SystemEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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

        var pauseResponse = await _client.PostAsync(ApiRoutes.Game.System.Pause, null);
        pauseResponse.EnsureSuccessStatusCode();
        Assert.Equal("1", await GetRedisValue(SystemControlConstants.MainLoopPausedKey));

        var resumeResponse = await _client.PostAsync(ApiRoutes.Game.System.Resume, null);
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

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(payload, received);
        await subscriber.DisposeAsync();
    }

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
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
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
