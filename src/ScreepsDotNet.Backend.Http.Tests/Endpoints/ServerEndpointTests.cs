namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using ScreepsDotNet.Backend.Http.Tests.Web;

public class ServerEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestHttpClient _client = new(factory.CreateClient());

    [Fact]
    public async Task ServerInfo_ReturnsConfiguredServerData()
    {
        var response = await _client.GetAsync(ApiRoutes.Server.Info);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;
        Assert.Equal(VersionTestValues.WelcomeText, root.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(VersionTestValues.HistoryChunkSize, root.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
    }

}
