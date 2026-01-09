namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
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
    private const string RoomOverviewEndpointName = "GetRoomOverview";
    private const string GenerateFlagNameEndpointName = "PostGenerateUniqueFlagName";
    private const string CheckFlagNameEndpointName = "PostCheckUniqueFlagName";
    private const string SetNotifyEndpointName = "PostSetNotifyWhenAttacked";
    private const string InvalidParamsMessage = "invalid params";
    private static readonly IReadOnlyDictionary<string, object?> EmptyDictionary = new Dictionary<string, object?>(StringComparer.Ordinal);

    public static void Map(WebApplication app)
    {
        MapMapStats(app);
        MapRoomStatus(app);
        MapRoomTerrain(app);
        MapRooms(app);
        MapWorldSize(app);
        MapTime(app);
        MapTick(app);
        MapRoomOverview(app);
        MapGenerateUniqueFlagName(app);
        MapCheckUniqueFlagName(app);
        MapSetNotifyWhenAttacked(app);
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
                          [FromQuery(Name = "shard")] string? shard,
                          IRoomStatusRepository repository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidRoomName(room))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              if (!RoomReferenceParser.TryParse(room, shard, out var reference) || reference is null)
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var status = await repository.GetRoomStatusAsync(reference.RoomName, reference.ShardName, cancellationToken).ConfigureAwait(false);
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
                          [FromQuery(Name = "shard")] string? shard,
                          [FromQuery(Name = "encoded")] string? encoded,
                          IRoomTerrainRepository repository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidRoomName(room))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              if (!RoomReferenceParser.TryParse(room, shard, out var reference) || reference is null)
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var entries = await repository.GetTerrainEntriesAsync([reference], cancellationToken).ConfigureAwait(false);
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

                               if (!RoomReferenceParser.TryParseRooms(request.Rooms, request.Shard, out var roomReferences))
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

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

    private static void MapRoomOverview(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.World.RoomOverview,
                   async ([FromQuery(Name = "room")] string? room,
                          [FromQuery(Name = "shard")] string? shard,
                          IRoomOverviewRepository overviewRepository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidRoomName(room))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              if (!RoomReferenceParser.TryParse(room, shard, out var reference) || reference is null)
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var overview = await overviewRepository.GetRoomOverviewAsync(reference, cancellationToken).ConfigureAwait(false);
                              var payload = CreateRoomOverviewResponse(overview);
                              return Results.Ok(payload);
                          })
           .RequireTokenAuthentication()
           .WithName(RoomOverviewEndpointName);
    }

    private static void MapGenerateUniqueFlagName(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.GenerateUniqueFlagName,
                    async (IFlagService flagService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               var name = await flagService.GenerateUniqueFlagNameAsync(accessor.CurrentUser.Id, cancellationToken).ConfigureAwait(false);
                               return Results.Ok(new UniqueFlagNameResponse(name));
                           })
           .RequireTokenAuthentication()
           .WithName(GenerateFlagNameEndpointName);
    }

    private static void MapCheckUniqueFlagName(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.CheckUniqueFlagName,
                    async ([FromBody] CheckFlagNameRequest request,
                           IFlagService flagService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Name))
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var unique = await flagService.IsFlagNameUniqueAsync(accessor.CurrentUser.Id, request.Name.Trim(), cancellationToken).ConfigureAwait(false);
                               if (!unique)
                                   return Results.BadRequest(new ErrorResponse("name exists"));

                               return Results.Ok(new { });
                           })
           .RequireTokenAuthentication()
           .WithName(CheckFlagNameEndpointName);
    }

    private static void MapSetNotifyWhenAttacked(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.SetNotifyWhenAttacked,
                    async ([FromBody] SetNotifyWhenAttackedRequest request,
                           INotifyWhenAttackedService notifyService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.StructureId) || request.Enabled is null)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var result = await notifyService.SetNotifyWhenAttackedAsync(request.StructureId.Trim(),
                                                                                           accessor.CurrentUser.Id,
                                                                                           request.Enabled.Value,
                                                                                           cancellationToken)
                                                              .ConfigureAwait(false);

                               return result.Status switch
                               {
                                   NotifyWhenAttackedResultStatus.Success => Results.Ok(new { ok = 1 }),
                                   NotifyWhenAttackedResultStatus.StructureNotFound => Results.BadRequest(new ErrorResponse("structure not found")),
                                   NotifyWhenAttackedResultStatus.NotOwner => Results.BadRequest(new ErrorResponse("not owner")),
                                   _ => Results.BadRequest(new ErrorResponse("error"))
                               };
                           })
           .RequireTokenAuthentication()
           .WithName(SetNotifyEndpointName);
    }

    private static bool IsValidRoomName(string? room)
        => !string.IsNullOrWhiteSpace(room);

    private static bool IsValidMapStatsRequest(MapStatsRequestModel? request, out IReadOnlyList<RoomReference> rooms)
    {
        rooms = Array.Empty<RoomReference>();
        if (request?.Rooms is null || !MapStatsRequestValidator.IsValid(request))
            return false;

        if (!RoomReferenceParser.TryParseRooms(request.Rooms, request.Shard, out var normalizedRooms))
            return false;

        if (string.IsNullOrWhiteSpace(request.StatName))
            return false;

        rooms = normalizedRooms;
        return true;
    }

    private static RoomOverviewResponse CreateRoomOverviewResponse(RoomOverview? overview)
    {
        if (overview?.Owner is null)
            return new RoomOverviewResponse(null, EmptyDictionary, EmptyDictionary, EmptyDictionary);

        var owner = new RoomOverviewOwnerResponse(overview.Owner.Id, overview.Owner.Username, overview.Owner.Badge);
        return new RoomOverviewResponse(owner, EmptyDictionary, EmptyDictionary, EmptyDictionary);
    }
}
