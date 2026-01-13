namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Globalization;
using System.Linq;
using System.Text.Json;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class MarketEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task OrdersIndex_ReturnsAggregatedCounts()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.OrdersIndex);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var summary = payload.RootElement.GetProperty("list").EnumerateArray().First();
        Assert.Equal("energy", summary.GetProperty("_id").GetString());
        Assert.Equal(2, summary.GetProperty("count").GetInt32());
        Assert.Equal(1, summary.GetProperty("buying").GetInt32());
        Assert.Equal(1, summary.GetProperty("selling").GetInt32());
    }

    [Fact]
    public async Task Orders_ReturnsScaledPrices()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.Game.Market.Orders}?resourceType=energy");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var order = payload.RootElement.GetProperty("list").EnumerateArray().First();
        Assert.Equal(5.0m, order.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task MyOrders_ReturnsPlayerOrdersOnly()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Game.Market.MyOrders);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var orders = payload.RootElement.GetProperty("list").EnumerateArray().ToList();
        Assert.Single(orders);
        Assert.Equal(SeedDataDefaults.User.Id, orders.First().GetProperty("user").GetString());
    }

    [Fact]
    public async Task Stats_ReturnsLatestFirst()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.Game.Market.Stats}?resourceType=energy");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var stats = payload.RootElement.GetProperty("stats").EnumerateArray().ToList();
        Assert.True(stats.Count >= 2);
        var firstDate = DateTime.ParseExact(stats[0].GetProperty("date").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var secondDate = DateTime.ParseExact(stats[1].GetProperty("date").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.True(firstDate >= secondDate);
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = SeedDataDefaults.Auth.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
