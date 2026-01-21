namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Text.Json;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class EngineEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<string> LoginAsync()
    {
        var request = new { ticket = SeedDataDefaults.Auth.Ticket };
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, request);
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        return content.GetProperty("token").GetString()!;
    }

    #region Status Endpoint

    [Fact]
    public async Task GetStatus_WithAuth_ReturnsStatistics()
    {
        // Arrange
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.GetAsync(ApiRoutes.Game.Engine.Status);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.True(content.TryGetProperty("totalRoomsProcessed", out _));
        Assert.True(content.TryGetProperty("averageProcessingTimeMs", out _));
        Assert.True(content.TryGetProperty("totalIntentsValidated", out _));
        Assert.True(content.TryGetProperty("rejectionRate", out _));
    }

    [Fact]
    public async Task GetStatus_NoAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync(ApiRoutes.Game.Engine.Status);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region RoomState Endpoint

    [Fact]
    public async Task GetRoomState_WithAuth_ReturnsState()
    {
        // Arrange
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);
        var roomName = "W1N1";

        // Act
        var response = await _client.GetAsync($"{ApiRoutes.Game.Engine.RoomState}?room={roomName}");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.True(content.TryGetProperty("roomName", out var roomNameProp));
        Assert.Equal(roomName, roomNameProp.GetString());
        Assert.True(content.TryGetProperty("gameTime", out _));
        Assert.True(content.TryGetProperty("objects", out _));
    }

    [Fact]
    public async Task GetRoomState_NoAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync($"{ApiRoutes.Game.Engine.RoomState}?room=W1N1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region ValidationStats Endpoint

    [Fact]
    public async Task GetValidationStats_WithAuth_ReturnsStats()
    {
        // Arrange
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.GetAsync(ApiRoutes.Game.Engine.ValidationStats);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.True(content.TryGetProperty("totalIntentsValidated", out _));
        Assert.True(content.TryGetProperty("validIntentsCount", out _));
        Assert.True(content.TryGetProperty("rejectedIntentsCount", out _));
    }

    [Fact]
    public async Task GetValidationStats_NoAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync(ApiRoutes.Game.Engine.ValidationStats);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region ValidationStatsReset Endpoint

    [Fact]
    public async Task PostValidationStatsReset_WithAuth_ClearsStats()
    {
        // Arrange
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Add("X-Token", token);

        // Act
        var response = await _client.PostAsync(ApiRoutes.Game.Engine.ValidationStatsReset, null!);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await TestHttpClient.ReadFromJsonAsync<JsonElement>(response);
        Assert.Equal(1, content.GetProperty("ok").GetInt32());
    }

    [Fact]
    public async Task PostValidationStatsReset_NoAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync(ApiRoutes.Game.Engine.ValidationStatsReset, null!);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
