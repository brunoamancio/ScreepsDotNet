using System.Net;
using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public sealed class MarketEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string ResourceTypeQuery = "?resourceType=energy";

    private readonly TestHttpClient _client = new(factory.CreateClient());

    [Fact]
    public async Task OrdersIndex_WithToken_ReturnsSummaries()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.OrdersIndex);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var list = payload.RootElement.GetProperty("list").EnumerateArray().ToList();
        Assert.NotEmpty(list);
        Assert.Equal("energy", list.First().GetProperty("_id").GetString());
    }

    [Fact]
    public async Task Orders_MissingResourceType_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.Orders);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Orders_WithResourceType_ReturnsOrders()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.Orders + ResourceTypeQuery);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var list = payload.RootElement.GetProperty("list").EnumerateArray().ToList();
        Assert.NotEmpty(list);
        Assert.Equal("energy", list.First().GetProperty("resourceType").GetString());
    }

    [Fact]
    public async Task MyOrders_WithToken_ReturnsUserOrders()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.MyOrders);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var list = payload.RootElement.GetProperty("list").EnumerateArray().ToList();
        Assert.Single(list);
        Assert.Equal(AuthTestValues.UserId, list.First().GetProperty("user").GetString());
    }

    [Fact]
    public async Task Stats_MissingResourceType_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.Stats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stats_WithResourceType_ReturnsEntries()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.Stats + ResourceTypeQuery);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var stats = payload.RootElement.GetProperty("stats").EnumerateArray().ToList();
        Assert.NotEmpty(stats);
        Assert.Equal("energy", stats.First().GetProperty("resourceType").GetString());
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
