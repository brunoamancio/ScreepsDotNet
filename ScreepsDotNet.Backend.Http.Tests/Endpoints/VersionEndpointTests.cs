using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;
using System.Text.Json;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class VersionEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public VersionEndpointTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Version_ReturnsExpectedShape()
    {
        var response = await _client.GetAsync(ApiRoutes.Version);

        response.EnsureSuccessStatusCode();
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal(VersionTestValues.Protocol, root.GetProperty(VersionResponseFields.Protocol).GetInt32());
        Assert.Equal(VersionTestValues.UseNativeAuth, root.GetProperty(VersionResponseFields.UseNativeAuth).GetBoolean());
        Assert.Equal(VersionTestValues.Users, root.GetProperty(VersionResponseFields.Users).GetInt32());
        Assert.Equal(VersionTestValues.PackageVersion, root.GetProperty(VersionResponseFields.PackageVersion).GetString());

        var serverData = root.GetProperty(VersionResponseFields.ServerData);
        Assert.Equal(VersionTestValues.WelcomeText, serverData.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(VersionTestValues.HistoryChunkSize, serverData.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
        Assert.Equal(VersionTestValues.SocketUpdateThrottle, serverData.GetProperty(ServerDataResponseFields.SocketUpdateThrottle).GetInt32());
        Assert.True(serverData.GetProperty(ServerDataResponseFields.CustomObjectTypes).EnumerateObject().MoveNext() == false);
    }
}
