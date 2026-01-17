using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public sealed class WorldEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string RoomQuery = "?room=W1N1";
    private const string EncodedQuery = "&encoded=1";
    private static readonly string[] SingleRoom = ["W1N1"];

    private readonly TestHttpClient _client = new(factory.CreateClient());

    [Fact]
    public async Task MapStats_InvalidBody_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.World.MapStats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { rooms = Array.Empty<string>(), statName = "owners1" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MapStats_InvalidStatName_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.World.MapStats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { rooms = SingleRoom, statName = "owners" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MapStats_ValidBody_ReturnsStats()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.World.MapStats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { rooms = SingleRoom, statName = "owners1" });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(12345, payload.RootElement.GetProperty("gameTime").GetInt32());
        var stats = payload.RootElement.GetProperty("stats");
        Assert.True(stats.TryGetProperty("W1N1", out var room));
        Assert.Equal("normal", room.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RoomStatus_MissingRoom_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.World.RoomStatus);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RoomStatus_WithRoom_ReturnsDetails()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.Game.World.RoomStatus}{RoomQuery}");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var room = payload.RootElement.GetProperty("room");
        Assert.Equal("normal", room.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RoomTerrain_MissingRoom_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.RoomTerrain);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RoomTerrain_EncodedFlag_ReturnsEncoding()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Game.World.RoomTerrain}{RoomQuery}{EncodedQuery}");

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var terrain = payload.RootElement.GetProperty("terrain").EnumerateArray().First();
        Assert.Equal(new string('0', 2500), terrain.GetProperty("terrain").GetString());
    }

    [Fact]
    public async Task RoomTerrain_Decoded_ReturnsTiles()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Game.World.RoomTerrain}{RoomQuery}");

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var terrain = payload.RootElement.GetProperty("terrain").EnumerateArray().First();
        var tiles = terrain.GetProperty("terrain").EnumerateArray().ToList();
        Assert.Equal(2500, tiles.Count);
        Assert.Equal("plain", tiles.First().GetProperty("terrain").GetString());
    }

    [Fact]
    public async Task Rooms_MissingBody_ReturnsBadRequest()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(ApiRoutes.Game.World.Rooms, content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rooms_WithRoomsArray_ReturnsEntries()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.Rooms, new { rooms = SingleRoom });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var rooms = payload.RootElement.GetProperty("rooms").EnumerateArray().ToList();
        Assert.Single(rooms);
    }

    [Fact]
    public async Task WorldSize_ReturnsMetadata()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.WorldSize);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(10, payload.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(10, payload.RootElement.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task Time_ReturnsMetadata()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.Time);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(999, payload.RootElement.GetProperty("time").GetInt32());
    }

    [Fact]
    public async Task Tick_ReturnsMetadata()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.Tick);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(500, payload.RootElement.GetProperty("tick").GetInt32());
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
