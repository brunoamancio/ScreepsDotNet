namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Models.Map;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class MapEndpoints
{
    private const string GenerateEndpointName = "PostMapGenerate";
    private const string OpenEndpointName = "PostMapOpen";
    private const string CloseEndpointName = "PostMapClose";
    private const string RemoveEndpointName = "PostMapRemove";
    private const string AssetsEndpointName = "PostMapAssetsUpdate";
    private const string TerrainEndpointName = "PostMapTerrainRefresh";

    private const string InvalidParamsMessage = "invalid params";

    public static void Map(WebApplication app)
    {
        MapGenerate(app);
        MapOpen(app);
        MapClose(app);
        MapRemove(app);
        MapAssetsUpdate(app);
        MapTerrainRefresh(app);
    }

    private static void MapGenerate(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Map.Generate,
                    async ([FromBody] MapGenerateRequest request,
                           IMapControlService mapControlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (!TryNormalizeRoom(request.Room, request.Shard, out var reference))
                                   return Results.BadRequest(new ErrorResponse("room is required"));

                               if (!ValidateGenerationRequest(request, out var preset, out var error))
                                   return Results.BadRequest(new ErrorResponse(error ?? InvalidParamsMessage));

                               var includeController = request.NoController is not true;
                               var includeKeeperLairs = request.KeeperLairs ?? false;
                               var options = new MapRoomGenerationOptions(reference.RoomName,
                                                                           preset,
                                                                           request.Sources ?? 2,
                                                                           includeController,
                                                                   includeKeeperLairs,
                                                                   request.MineralType,
                                                                   request.Overwrite ?? false,
                                                                   request.Seed);
                               try {
                                   var result = await mapControlService.GenerateRoomAsync(options, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new MapGenerateResponse(result));
                               }
                               catch (InvalidOperationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(GenerateEndpointName);
    }

    private static void MapOpen(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Map.Open,
                    async ([FromBody] MapRoomRequest request,
                           IMapControlService mapControlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken)
                        => await HandleRoomToggleAsync(request, mapControlService.OpenRoomAsync, accessor, cancellationToken).ConfigureAwait(false))
           .RequireTokenAuthentication()
           .WithName(OpenEndpointName);
    }

    private static void MapClose(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Map.Close,
                    async ([FromBody] MapRoomRequest request,
                           IMapControlService mapControlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               return await HandleRoomToggleAsync(request,
                                                                  mapControlService.CloseRoomAsync,
                                                                  accessor,
                                                                  cancellationToken)
                                              .ConfigureAwait(false);
                           })
           .RequireTokenAuthentication()
           .WithName(CloseEndpointName);
    }

    private static void MapRemove(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Map.Remove,
                    async ([FromBody] MapRemoveRequest request,
                           IMapControlService mapControlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (!TryNormalizeRoom(request.Room, request.Shard, out var reference))
                                   return Results.BadRequest(new ErrorResponse("room is required"));

                               try {
                                   await mapControlService.RemoveRoomAsync(reference.RoomName, request.PurgeObjects ?? false, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (InvalidOperationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(RemoveEndpointName);
    }

    private static void MapAssetsUpdate(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Map.AssetsUpdate,
                    async ([FromBody] MapAssetsRequest request,
                           IMapControlService mapControlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (!TryNormalizeRoom(request.Room, request.Shard, out var reference))
                                   return Results.BadRequest(new ErrorResponse("room is required"));

                               try {
                                   await mapControlService.UpdateRoomAssetsAsync(reference.RoomName, request.Full ?? false, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (InvalidOperationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(AssetsEndpointName);
    }

    private static void MapTerrainRefresh(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Map.TerrainRefresh,
                    async (IMapControlService mapControlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) => {
                               if (accessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               await mapControlService.RefreshTerrainCacheAsync(cancellationToken).ConfigureAwait(false);
                               return Results.Ok(new { ok = 1 });
                           })
           .RequireTokenAuthentication()
           .WithName(TerrainEndpointName);
    }

    private static async Task<IResult> HandleRoomToggleAsync(MapRoomRequest request,
                                                             Func<string, CancellationToken, Task> handler,
                                                             ICurrentUserAccessor accessor,
                                                             CancellationToken cancellationToken)
    {
        if (accessor.CurrentUser?.Id is null)
            return Results.Unauthorized();

        if (!TryNormalizeRoom(request.Room, request.Shard, out var reference))
            return Results.BadRequest(new ErrorResponse("room is required"));

        try {
            await handler(reference.RoomName, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { ok = 1 });
        }
        catch (InvalidOperationException ex) {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    private static bool ValidateGenerationRequest(MapGenerateRequest request, out MapTerrainPreset preset, out string? error)
    {
        preset = MapTerrainPreset.Mixed;
        if (string.IsNullOrWhiteSpace(request.Room)) {
            error = "room is required";
            return false;
        }

        var sources = request.Sources ?? 2;
        if (sources is < 1 or > 5) {
            error = "sources must be between 1 and 5";
            return false;
        }

        var presetValue = string.IsNullOrWhiteSpace(request.Terrain) ? nameof(MapTerrainPreset.Mixed) : request.Terrain;
        if (!Enum.TryParse(presetValue, ignoreCase: true, out preset)) {
            error = "invalid terrain preset";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryNormalizeRoom(string? room, string? shard, [NotNullWhen(true)] out RoomReference? reference)
    {
        reference = null;
        if (!RoomReferenceParser.TryParse(room, shard, out var parsed) || parsed is null)
            return false;

        reference = parsed;
        return true;
    }

    private sealed record MapGenerateRequest(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("sources")] int? Sources,
        [property: JsonPropertyName("terrain")] string? Terrain,
        [property: JsonPropertyName("noController")] bool? NoController,
        [property: JsonPropertyName("keeperLairs")] bool? KeeperLairs,
        [property: JsonPropertyName("mineralType")] string? MineralType,
        [property: JsonPropertyName("overwrite")] bool? Overwrite,
        [property: JsonPropertyName("seed")] int? Seed);

    private sealed record MapGenerateResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("objects")] int ObjectCount,
        [property: JsonPropertyName("sources")] int SourceCount,
        [property: JsonPropertyName("controller")] bool ControllerCreated,
        [property: JsonPropertyName("keeperLairs")] bool KeeperLairsCreated,
        [property: JsonPropertyName("mineral")] string? MineralType)
    {
        public MapGenerateResponse(MapGenerationResult result)
            : this(1,
                   result.RoomName,
                   result.ObjectCount,
                   result.SourceCount,
                   result.ControllerCreated,
                   result.KeeperLairsCreated,
                   result.MineralType)
        {
        }
    }

    private sealed record MapRoomRequest(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard);

    private sealed record MapRemoveRequest(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("purgeObjects")] bool? PurgeObjects);

    private sealed record MapAssetsRequest(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("full")] bool? Full);
}
