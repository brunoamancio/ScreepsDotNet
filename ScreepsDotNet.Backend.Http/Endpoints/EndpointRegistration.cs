using ScreepsDotNet.Backend.Core.Services;

namespace ScreepsDotNet.Backend.Http.Endpoints;

internal static class EndpointRegistration
{
    private const string ServerInfoRoute = "/api/server/info";
    private const string ServerInfoEndpointName = "GetServerInfo";

    public static void MapBackendEndpoints(this WebApplication app)
    {
        app.MapGet(ServerInfoRoute, (IServerInfoProvider provider) => Results.Ok(provider.GetServerInfo()))
           .WithName(ServerInfoEndpointName);
    }
}
