namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;

public class ServerEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ServerEndpointTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task ServerInfo_ReturnsConfiguredServerData()
    {
        var response = await _client.GetAsync(ApiRoutes.Server.Info);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal(VersionTestValues.WelcomeText, root.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(VersionTestValues.HistoryChunkSize, root.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
    }

}
