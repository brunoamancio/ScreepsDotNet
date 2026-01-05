namespace ScreepsDotNet.Backend.Http.Endpoints;

internal static class EndpointRegistration
{
    public static void MapBackendEndpoints(this WebApplication app)
    {
        VersionEndpoints.Map(app);
        AuthEndpoints.Map(app);
        UserEndpoints.Map(app);
    }
}
