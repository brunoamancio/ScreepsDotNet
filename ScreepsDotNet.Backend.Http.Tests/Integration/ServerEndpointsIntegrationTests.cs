namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Text.Json;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class ServerEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ServerInfo_ReturnsMongoValues()
    {
        var response = await _client.GetAsync(ApiRoutes.Server.Info);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal(SeedDataDefaults.ServerData.WelcomeText, root.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(SeedDataDefaults.ServerData.HistoryChunkSize, root.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
        Assert.Equal(SeedDataDefaults.ServerData.SocketUpdateThrottle, root.GetProperty(ServerDataResponseFields.SocketUpdateThrottle).GetInt32());
    }

    [Fact]
    public async Task VersionEndpoint_ReflectsMongoServerData()
    {
        var response = await _client.GetAsync(ApiRoutes.Version);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var serverData = payload.RootElement.GetProperty(VersionResponseFields.ServerData);
        Assert.Equal(SeedDataDefaults.ServerData.WelcomeText, serverData.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(SeedDataDefaults.ServerData.HistoryChunkSize, serverData.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
    }
}
