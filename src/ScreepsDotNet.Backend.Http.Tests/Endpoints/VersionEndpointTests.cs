using System.Text.Json;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class VersionEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestHttpClient _client = new(factory.CreateClient());

    [Fact]
    public async Task Version_ReturnsExpectedShape()
    {
        var response = await _client.GetAsync(ApiRoutes.Version);

        response.EnsureSuccessStatusCode();
        var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;

        Assert.Equal(VersionTestValues.Protocol, root.GetProperty(VersionResponseFields.Protocol).GetInt32());
        Assert.Equal(VersionTestValues.UseNativeAuth, root.GetProperty(VersionResponseFields.UseNativeAuth).GetBoolean());
        Assert.Equal(VersionTestValues.Users, root.GetProperty(VersionResponseFields.Users).GetInt32());
        Assert.Equal(VersionTestValues.PackageVersion, root.GetProperty(VersionResponseFields.PackageVersion).GetString());

        var serverData = root.GetProperty(VersionResponseFields.ServerData);
        Assert.Equal(VersionTestValues.WelcomeText, serverData.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(VersionTestValues.HistoryChunkSize, serverData.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
        Assert.Equal(VersionTestValues.SocketUpdateThrottle, serverData.GetProperty(ServerDataResponseFields.SocketUpdateThrottle).GetInt32());
        Assert.False(serverData.GetProperty(ServerDataResponseFields.CustomObjectTypes).EnumerateObject().MoveNext());
    }
}
