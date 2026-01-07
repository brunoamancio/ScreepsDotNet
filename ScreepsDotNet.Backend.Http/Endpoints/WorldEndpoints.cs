namespace ScreepsDotNet.Backend.Http.Endpoints;

using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Routing;

internal static class WorldEndpoints
{
    private const string MapStatsEndpointName = "PostWorldMapStats";
    private const string RoomStatusEndpointName = "GetWorldRoomStatus";
    private const string RoomTerrainEndpointName = "GetWorldRoomTerrain";
    private const string RoomsEndpointName = "PostWorldRooms";
    private const string WorldSizeEndpointName = "GetWorldWorldSize";
    private const string TimeEndpointName = "GetWorldTime";
    private const string TickEndpointName = "GetWorldTick";

    public static void Map(WebApplication app)
    {
        MapProtectedPost(app, ApiRoutes.Game.World.MapStats, MapStatsEndpointName);
        MapProtectedGet(app, ApiRoutes.Game.World.RoomStatus, RoomStatusEndpointName);
        MapPublicGet(app, ApiRoutes.Game.World.RoomTerrain, RoomTerrainEndpointName);
        MapPublicPost(app, ApiRoutes.Game.World.Rooms, RoomsEndpointName);
        MapPublicGet(app, ApiRoutes.Game.World.WorldSize, WorldSizeEndpointName);
        MapPublicGet(app, ApiRoutes.Game.World.Time, TimeEndpointName);
        MapPublicGet(app, ApiRoutes.Game.World.Tick, TickEndpointName);
    }

    private static void MapProtectedGet(WebApplication app, string route, string endpointName)
        => app.MapGet(route, () => EndpointStubResults.NotImplemented(route))
              .RequireTokenAuthentication()
              .WithName(endpointName);

    private static void MapProtectedPost(WebApplication app, string route, string endpointName)
        => app.MapPost(route, () => EndpointStubResults.NotImplemented(route))
              .RequireTokenAuthentication()
              .WithName(endpointName);

    private static void MapPublicGet(WebApplication app, string route, string endpointName)
        => app.MapGet(route, () => EndpointStubResults.NotImplemented(route))
              .WithName(endpointName);

    private static void MapPublicPost(WebApplication app, string route, string endpointName)
        => app.MapPost(route, () => EndpointStubResults.NotImplemented(route))
              .WithName(endpointName);
}
