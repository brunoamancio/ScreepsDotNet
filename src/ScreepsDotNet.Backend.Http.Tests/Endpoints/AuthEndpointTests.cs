using System.Net;
using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

[Trait("Category", "Integration")]
public class AuthEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string InvalidTokenValue = "invalid";

    private readonly TestHttpClient _client = new(factory.CreateClient());

    [Fact]
    public async Task SteamTicket_ReturnsTokenAndSteamId()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty(AuthResponseFields.Token).GetString()));
        Assert.Equal(AuthTestValues.SteamId, root.GetProperty(AuthResponseFields.SteamId).GetString());
    }

    [Fact]
    public async Task SteamTicket_UnsupportedAuthPath_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(AuthResponseMessages.UnsupportedAuthMethod, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task SteamTicket_InvalidTicket_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = "invalid-ticket",
            useNativeAuth = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(AuthResponseMessages.CouldNotAuthenticate, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task AuthMe_MissingTokenHeader_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(ApiRoutes.AuthMe);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(AuthResponseMessages.Unauthorized, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task AuthMe_InvalidToken_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.AuthMe);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, InvalidTokenValue);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(AuthResponseMessages.Unauthorized, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task AuthMe_ValidToken_ReturnsProfileAndRefreshesHeader()
    {
        var loginResponse = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = false
        });
        loginResponse.EnsureSuccessStatusCode();
        using var loginPayload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(loginResponse));
        var token = loginPayload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.AuthMe);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues(AuthHeaderNames.Token, out var refreshedTokens));
        Assert.False(string.IsNullOrWhiteSpace(refreshedTokens.Single()));

        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;
        Assert.Equal(AuthTestValues.UserId, root.GetProperty(AuthResponseFields.UserId).GetString());
        Assert.Equal(AuthTestValues.Username, root.GetProperty(AuthResponseFields.Username).GetString());
        Assert.Equal(AuthTestValues.Email, root.GetProperty(AuthResponseFields.Email).GetString());
        Assert.True(root.GetProperty(AuthResponseFields.Password).GetBoolean());
        var steam = root.GetProperty(AuthResponseFields.Steam);
        Assert.Equal(AuthTestValues.SteamId, steam.GetProperty(AuthResponseFields.SteamFields.Id).GetString());
    }
}
