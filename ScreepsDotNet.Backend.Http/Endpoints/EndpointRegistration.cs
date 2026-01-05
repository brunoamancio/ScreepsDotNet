namespace ScreepsDotNet.Backend.Http.Endpoints;

internal static class EndpointRegistration
{
    private const string HealthRoute = "/health";
    private const string HealthEndpointName = "GetHealth";

    private const string ServerInfoRoute = "/api/server/info";
    private const string ServerInfoEndpointName = "GetServerInfo";

    public static void MapBackendEndpoints(this WebApplication app)
    {
        var serverInfo = app.Configuration.GetSection(ServerInfoOptions.SectionName).Get<ServerInfoOptions>()
                          ?? new ServerInfoOptions();

        // TODO: Wire to real health checks once storage and engine integrations exist.
        app.MapGet(HealthRoute, () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }))
           .WithName(HealthEndpointName);

        // TODO: Replace with real data retrieved from storage/engine modules.
        app.MapGet(ServerInfoRoute, () => Results.Ok(serverInfo))
           .WithName(ServerInfoEndpointName);
    }
}

internal sealed record ServerInfoOptions
{
    public const string SectionName = "ServerInfo";

    public string Name { get; init; } = "ScreepsDotNet";

    public string Build { get; init; } = "0.0.1-dev";

    public bool CliEnabled { get; init; }
}
