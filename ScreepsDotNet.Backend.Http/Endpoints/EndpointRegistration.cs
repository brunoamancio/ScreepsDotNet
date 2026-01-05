using ScreepsDotNet.Backend.Core.Services;

namespace ScreepsDotNet.Backend.Http.Endpoints;

using ScreepsDotNet.Backend.Http.Routing;

internal static class EndpointRegistration
{
    private const string ServerInfoEndpointName = "GetServerInfo";

    public static void MapBackendEndpoints(this WebApplication app)
    {
        app.MapGet(ApiRoutes.ServerInfo, (IServerInfoProvider provider) => Results.Ok(provider.GetServerInfo()))
           .WithName(ServerInfoEndpointName);
    }
}
