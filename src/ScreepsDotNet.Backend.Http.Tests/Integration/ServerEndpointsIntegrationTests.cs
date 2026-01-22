namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Text.Json;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ServerEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ServerInfo_ReturnsMongoValues()
    {
        var response = await _client.GetAsync(ApiRoutes.Server.Info);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;
        Assert.Equal(SeedDataDefaults.ServerData.WelcomeText, root.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(SeedDataDefaults.ServerData.HistoryChunkSize, root.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
        Assert.Equal(SeedDataDefaults.ServerData.SocketUpdateThrottle, root.GetProperty(ServerDataResponseFields.SocketUpdateThrottle).GetInt32());
        var customTypes = root.GetProperty(ServerDataResponseFields.CustomObjectTypes);
        Assert.True(customTypes.TryGetProperty("shield", out var shield));
        Assert.Equal("#00bfff", shield.GetProperty("color").GetString());
        Assert.Equal(500, shield.GetProperty("decay").GetInt32());
        Assert.Equal(500, shield.GetProperty("decay").GetInt32());
    }

    [Fact]
    public async Task VersionEndpoint_ReflectsMongoServerData()
    {
        var response = await _client.GetAsync(ApiRoutes.Version);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;
        Assert.Equal(SeedDataDefaults.Version.Protocol, root.GetProperty(VersionResponseFields.Protocol).GetInt32());
        Assert.Equal(SeedDataDefaults.Version.UseNativeAuth, root.GetProperty(VersionResponseFields.UseNativeAuth).GetBoolean());
        var serverData = root.GetProperty(VersionResponseFields.ServerData);
        Assert.Equal(SeedDataDefaults.ServerData.WelcomeText, serverData.GetProperty(ServerDataResponseFields.WelcomeText).GetString());
        Assert.Equal(SeedDataDefaults.ServerData.HistoryChunkSize, serverData.GetProperty(ServerDataResponseFields.HistoryChunkSize).GetInt32());
        var customTypes = serverData.GetProperty(ServerDataResponseFields.CustomObjectTypes);
        Assert.True(customTypes.TryGetProperty("shield", out var shield));
        Assert.Equal("#00bfff", shield.GetProperty("color").GetString());
    }
}
