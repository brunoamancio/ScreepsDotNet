using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;
using System.Text.Json;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class ServerInfoEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ServerInfoEndpointTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task ServerInfo_ReturnsExpectedData()
    {
        var response = await _client.GetAsync(ApiRoutes.ServerInfo);

        response.EnsureSuccessStatusCode();
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal("Test Server", root.GetProperty(ServerInfoResponseFields.Name).GetString());
        Assert.Equal("test-build", root.GetProperty(ServerInfoResponseFields.Build).GetString());
        Assert.True(root.GetProperty(ServerInfoResponseFields.CliEnabled).GetBoolean());
    }
}
