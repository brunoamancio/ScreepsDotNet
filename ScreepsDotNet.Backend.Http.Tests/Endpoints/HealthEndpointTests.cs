using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ScreepsDotNet.Backend.Http.Health;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class HealthEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsHealthyPayload()
    {
        var response = await _client.GetAsync(HealthCheckOptionsFactory.HealthEndpoint);

        response.EnsureSuccessStatusCode();
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal(nameof(HealthStatus.Healthy), root.GetProperty(HealthResponseFields.Status).GetString());

        var storage = root.GetProperty(HealthResponseFields.Results).GetProperty(StorageHealthCheck.HealthCheckName);
        Assert.Equal(nameof(HealthStatus.Healthy), storage.GetProperty(HealthResponseFields.Status).GetString());
    }
}
