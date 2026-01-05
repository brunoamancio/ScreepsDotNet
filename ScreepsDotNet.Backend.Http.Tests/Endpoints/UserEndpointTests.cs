using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class UserEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserEndpointTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task WorldStartRoom_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(ApiRoutes.User.WorldStartRoom);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(AuthResponseMessages.Unauthorized, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task WorldStartRoom_WithToken_ReturnsNotImplemented()
    {
        var loginResponse = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = false
        });
        loginResponse.EnsureSuccessStatusCode();
        using var loginPayload = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var token = loginPayload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.WorldStartRoom);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
