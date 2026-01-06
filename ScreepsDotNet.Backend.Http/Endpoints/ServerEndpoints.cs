namespace ScreepsDotNet.Backend.Http.Endpoints;

using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Routing;

internal static class ServerEndpoints
{
    private const string ServerInfoEndpointName = "GetServerInfo";

    public static void Map(WebApplication app)
    {
        app.MapGet(ApiRoutes.Server.Info,
                   async (IServerDataRepository repository, CancellationToken cancellationToken) =>
                   {
                       var data = await repository.GetServerDataAsync(cancellationToken).ConfigureAwait(false);
                       return Results.Ok(data);
                   })
           .WithName(ServerInfoEndpointName);
    }
}
