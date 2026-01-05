namespace ScreepsDotNet.Backend.Http.Endpoints;

using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Routing;

internal static class EndpointRegistration
{
    private const string VersionEndpointName = "GetVersion";

    public static void MapBackendEndpoints(this WebApplication app)
    {
        app.MapGet(ApiRoutes.Version, async (IVersionInfoProvider provider, CancellationToken cancellationToken) =>
                   {
                       var payload = await provider.GetAsync(cancellationToken).ConfigureAwait(false);
                       return Results.Ok(payload);
                   })
           .WithName(VersionEndpointName);
    }
}
