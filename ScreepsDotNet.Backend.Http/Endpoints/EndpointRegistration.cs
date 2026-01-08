namespace ScreepsDotNet.Backend.Http.Endpoints;

internal static class EndpointRegistration
{
    public static void MapBackendEndpoints(this WebApplication app)
    {
        VersionEndpoints.Map(app);
        AuthEndpoints.Map(app);
        ServerEndpoints.Map(app);
        UserEndpoints.Map(app);
        MarketEndpoints.Map(app);
        WorldEndpoints.Map(app);
        SpawnEndpoints.Map(app);
        ConstructionEndpoints.Map(app);
        FlagEndpoints.Map(app);
        InvaderEndpoints.Map(app);
        IntentEndpoints.Map(app);
        BotEndpoints.Map(app);
        StrongholdEndpoints.Map(app);
        SystemEndpoints.Map(app);
        MapEndpoints.Map(app);
    }
}
