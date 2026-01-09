namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Helpers;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
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
    private const string InvalidParamsMessage = "invalid params";

    public static void Map(WebApplication app)
    {
        MapMapStats(app);
        MapRoomStatus(app);
        MapRoomTerrain(app);
        MapRooms(app);
        MapWorldSize(app);
        MapTime(app);
        MapTick(app);
    }

    private static void MapMapStats(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.MapStats,
                    async ([FromBody] MapStatsRequestModel? request,
                           IWorldStatsRepository repository,
                           CancellationToken cancellationToken) => {
                               if (!IsValidMapStatsRequest(request, out var rooms))
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var stats = await repository.GetMapStatsAsync(new MapStatsRequest(rooms, request!.StatName!.Trim()),
                                                                      cancellationToken)
                                                    .ConfigureAwait(false);
                               var payload = WorldResponseFactory.CreateMapStatsResponse(stats);
                               return Results.Ok(payload);
                           })
           .RequireTokenAuthentication()
           .WithName(MapStatsEndpointName);
    }

    private static void MapRoomStatus(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.World.RoomStatus,
                   async ([FromQuery(Name = "room")] string? room,
                          IRoomStatusRepository repository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidRoomName(room))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var status = await repository.GetRoomStatusAsync(room!, null, cancellationToken).ConfigureAwait(false);
                              var payload = WorldResponseFactory.CreateRoomStatusResponse(status);
                              return Results.Ok(payload);
                          })
           .RequireTokenAuthentication()
           .WithName(RoomStatusEndpointName);
    }

    private static void MapRoomTerrain(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.World.RoomTerrain,
                   async ([FromQuery(Name = "room")] string? room,
                          [FromQuery(Name = "encoded")] string? encoded,
                          IRoomTerrainRepository repository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidRoomName(room))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var entries = await repository.GetTerrainEntriesAsync([RoomReference.Create(room!)], cancellationToken).ConfigureAwait(false);
                              var payload = string.IsNullOrEmpty(encoded)
                           ? WorldResponseFactory.CreateDecodedTerrainResponse(entries)
                           : WorldResponseFactory.CreateEncodedTerrainResponse(entries);
                              return Results.Ok(payload);
                          })
           .WithName(RoomTerrainEndpointName);
    }

    private static void MapRooms(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.Rooms,
                    async ([FromBody] RoomsRequest? request,
                           IRoomTerrainRepository repository,
                           CancellationToken cancellationToken) => {
                               if (request?.Rooms is null || request.Rooms.Count == 0)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var rooms = request.Rooms.Where(static name => !string.IsNullOrWhiteSpace(name))
                                                 .Select(static name => name.Trim())
                                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                                 .ToList();

                               if (rooms.Count == 0)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var roomReferences = rooms.Select(static room => RoomReference.Create(room)).ToList();
                               var entries = await repository.GetTerrainEntriesAsync(roomReferences, cancellationToken).ConfigureAwait(false);
                               var payload = WorldResponseFactory.CreateRoomsResponse(entries);
                               return Results.Ok(payload);
                           })
           .WithName(RoomsEndpointName);
    }

    private static void MapWorldSize(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.World.WorldSize,
                   async (IWorldMetadataRepository repository, CancellationToken cancellationToken) => {
                       var size = await repository.GetWorldSizeAsync(cancellationToken).ConfigureAwait(false);
                       var payload = WorldResponseFactory.CreateWorldSizeResponse(size);
                       return Results.Ok(payload);
                   })
           .WithName(WorldSizeEndpointName);
    }

    private static void MapTime(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.World.Time,
                   async (IWorldMetadataRepository repository, CancellationToken cancellationToken) => {
                       var time = await repository.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
                       return Results.Ok(WorldResponseFactory.CreateTimeResponse(time));
                   })
           .WithName(TimeEndpointName);
    }

    private static void MapTick(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.World.Tick,
                   async (IWorldMetadataRepository repository, CancellationToken cancellationToken) => {
                       var tick = await repository.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);
                       return Results.Ok(WorldResponseFactory.CreateTickResponse(tick));
                   })
           .WithName(TickEndpointName);
    }

    private static bool IsValidRoomName(string? room)
        => !string.IsNullOrWhiteSpace(room);

    private static bool IsValidMapStatsRequest(MapStatsRequestModel? request, out IReadOnlyList<RoomReference> rooms)
    {
        rooms = Array.Empty<RoomReference>();
        if (request?.Rooms is null || !MapStatsRequestValidator.IsValid(request))
            return false;

        var normalizedRooms = request.Rooms.Where(static name => !string.IsNullOrWhiteSpace(name))
                                           .Select(static name => name.Trim())
                                           .Distinct(StringComparer.OrdinalIgnoreCase)
                                           .Select(static name => RoomReference.Create(name))
                                           .ToList();

        if (normalizedRooms.Count == 0 || string.IsNullOrWhiteSpace(request.StatName))
            return false;

        rooms = normalizedRooms;
        return true;
    }
}
