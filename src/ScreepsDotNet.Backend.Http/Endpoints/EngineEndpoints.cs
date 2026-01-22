namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Validation;

internal static class EngineEndpoints
{
    private const string StatusEndpointName = "GetEngineStatus";
    private const string RoomStateEndpointName = "GetEngineRoomState";
    private const string ValidationStatsEndpointName = "GetEngineValidationStats";
    private const string ValidationStatsResetEndpointName = "PostEngineValidationStatsReset";

#pragma warning disable IDE0051, IDE0052 // Used in attribute parameters
    private const string RoomQueryName = "room";
#pragma warning restore IDE0051, IDE0052

    public static void Map(WebApplication app)
    {
        MapStatus(app);
        MapRoomState(app);
        MapValidationStats(app);
        MapValidationStatsReset(app);
    }

    private static void MapStatus(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Engine.Status,
                   async (IEngineDiagnosticsService diagnosticsService,
                          ICurrentUserAccessor accessor,
                          CancellationToken cancellationToken) => {
                              if (accessor.CurrentUser?.Id is null)
                                  return Results.Unauthorized();

                              var stats = await diagnosticsService.GetEngineStatisticsAsync(cancellationToken).ConfigureAwait(false);
                              return Results.Ok(stats);
                          })
           .RequireTokenAuthentication()
           .WithName(StatusEndpointName);
    }

    private static void MapRoomState(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Engine.RoomState,
                   async ([FromQuery(Name = RoomQueryName)] string? room,
                          IRoomStateProvider stateProvider,
                          ICurrentUserAccessor accessor,
                          CancellationToken cancellationToken) => {
                              if (accessor.CurrentUser?.Id is null)
                                  return Results.Unauthorized();

                              if (string.IsNullOrWhiteSpace(room))
                                  return Results.BadRequest(new { error = "room parameter required" });

                              var state = await stateProvider.GetRoomStateAsync(room, gameTime: 0, token: cancellationToken).ConfigureAwait(false);
                              return Results.Ok(state);
                          })
           .RequireTokenAuthentication()
           .WithName(RoomStateEndpointName);
    }

    private static void MapValidationStats(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Engine.ValidationStats,
                   (IValidationStatisticsSink statisticsSink,
                    ICurrentUserAccessor accessor) => {
                        if (accessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        var stats = statisticsSink.GetStatistics();
                        return Results.Ok(stats);
                    })
           .RequireTokenAuthentication()
           .WithName(ValidationStatsEndpointName);
    }

    private static void MapValidationStatsReset(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Engine.ValidationStatsReset,
                    (IValidationStatisticsSink statisticsSink,
                     ICurrentUserAccessor accessor) => {
                         if (accessor.CurrentUser?.Id is null)
                             return Results.Unauthorized();

                         statisticsSink.Reset();
                         return Results.Ok(new { ok = 1 });
                     })
           .RequireTokenAuthentication()
           .WithName(ValidationStatsResetEndpointName);
    }
}
