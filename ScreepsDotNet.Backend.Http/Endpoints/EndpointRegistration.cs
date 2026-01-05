using ScreepsDotNet.Backend.Core.Services;

namespace ScreepsDotNet.Backend.Http.Endpoints;

internal static class EndpointRegistration
{
    private const string HealthRoute = "/health";
    private const string HealthEndpointName = "GetHealth";

    private const string ServerInfoRoute = "/api/server/info";
    private const string ServerInfoEndpointName = "GetServerInfo";

    public static void MapBackendEndpoints(this WebApplication app)
    {
        // TODO: Wire to real health checks once storage and engine integrations exist.
        app.MapGet(HealthRoute, () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }))
           .WithName(HealthEndpointName);

        app.MapGet(ServerInfoRoute, (IServerInfoProvider provider) => Results.Ok(provider.GetServerInfo()))
           .WithName(ServerInfoEndpointName);
    }
}
