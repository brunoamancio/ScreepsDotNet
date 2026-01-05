using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class UserEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FakeUserWorldRepository _userWorldRepository;
    private const string CustomControllerRoom = "W12N3";

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
        _userWorldRepository.WorldStatus = UserWorldStatus.Lost;
        var token = await AuthenticateAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.WorldStatus);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var expectedStatus = nameof(UserWorldStatus.Lost).ToLowerInvariant();
        Assert.Equal(expectedStatus, payload.RootElement.GetProperty(UserResponseFields.Status).GetString());
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
