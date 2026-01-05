using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class UserEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FakeUserWorldRepository _userWorldRepository;

    private const string CustomControllerRoom = "W12N3";
    private const string RoomsQueryUserId = "user-1";
    private const string StatsValidInterval = "8";
    private const string StatsInvalidInterval = "1";
    private static readonly string[] SampleRooms = ["W1N1", "W2N2"];
    private const string UsernameQueryParameter = "?username=TestUser";
    private const string RoomsQueryParameter = "?id=" + RoomsQueryUserId;
    private const string StatsValidQueryParameter = "?interval=" + StatsValidInterval;
    private const string StatsInvalidQueryParameter = "?interval=" + StatsInvalidInterval;

    public UserEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var services = factory.Services;
        _userWorldRepository = (FakeUserWorldRepository)services.GetRequiredService<IUserWorldRepository>();
    }

    [Fact]
    public async Task WorldStartRoom_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(ApiRoutes.User.WorldStartRoom);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(AuthResponseMessages.Unauthorized, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task WorldStartRoom_WithToken_ReturnsRoom()
    {
        _userWorldRepository.ControllerRoom = CustomControllerRoom;
        var token = await AuthenticateAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.WorldStartRoom);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rooms = payload.RootElement.GetProperty(UserResponseFields.Room).EnumerateArray().Select(element => element.GetString()).ToList();
        Assert.Contains(CustomControllerRoom, rooms);
    }

    [Fact]
    public async Task WorldStatus_WithToken_ReturnsStatus()
    {
        _userWorldRepository.WorldStatus = Core.Models.UserWorldStatus.Lost;
        var token = await AuthenticateAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.WorldStatus);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var expectedStatus = nameof(Core.Models.UserWorldStatus.Lost).ToLowerInvariant();
        Assert.Equal(expectedStatus, payload.RootElement.GetProperty(UserResponseFields.Status).GetString());
    }

    [Fact]
    public async Task UserFind_WithoutParams_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Find);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserFind_WithUsername_ReturnsProfile()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Find + UsernameQueryParameter);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var userElement = payload.RootElement.GetProperty(UserResponseFields.User);
        Assert.Equal(AuthTestValues.UserId, userElement.GetProperty(UserResponseFields.Id).GetString());
    }

    [Fact]
    public async Task UserRooms_WithoutUserId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Rooms);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserRooms_WithUserId_ReturnsRooms()
    {
        _userWorldRepository.ControllerRooms = SampleRooms;

        var response = await _client.GetAsync(ApiRoutes.User.Rooms + RoomsQueryParameter);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rooms = payload.RootElement.GetProperty(UserResponseFields.Rooms).EnumerateArray().Select(element => element.GetString()).ToList();
        Assert.Contains(SampleRooms[0], rooms);
        Assert.Contains(SampleRooms[1], rooms);
    }

    [Fact]
    public async Task UserStats_InvalidInterval_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Stats + StatsInvalidQueryParameter);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserStats_ValidInterval_ReturnsStats()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Stats + StatsValidQueryParameter);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Stats, out _));
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
