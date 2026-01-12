namespace ScreepsDotNet.Backend.Http.Endpoints;

using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Routing;

internal static class ServerEndpoints
{
    private const string ServerInfoEndpointName = "GetServerInfo";

    public static void Map(WebApplication app)
    {
        app.MapGet(ApiRoutes.Server.Info,
                   async (IServerDataRepository repository,
                          IModManifestProvider manifestProvider,
                          CancellationToken cancellationToken) => {
                       var data = await repository.GetServerDataAsync(cancellationToken).ConfigureAwait(false);
                       var manifest = await manifestProvider.GetManifestAsync(cancellationToken).ConfigureAwait(false);
                       var merged = data.WithCustomObjectOverrides(manifest.CustomObjectTypes);
                       return Results.Ok(merged);
                   })
           .WithName(ServerInfoEndpointName);
    }
}
