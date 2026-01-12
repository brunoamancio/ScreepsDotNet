namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class StrongholdEndpoints
{
    private const string TemplatesEndpointName = "GetStrongholdTemplates";
    private const string SpawnEndpointName = "PostStrongholdSpawn";
    private const string ExpandEndpointName = "PostStrongholdExpand";
    private const string InvalidParamsMessage = "invalid params";
    private const string StrongholdNotFoundMessage = "stronghold not found";

    public static void Map(WebApplication app)
    {
        MapTemplates(app);
        MapSpawn(app);
        MapExpand(app);
    }

    private static void MapTemplates(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Stronghold.Templates,
                   async (IStrongholdTemplateProvider templateProvider,
                          ICurrentUserAccessor userAccessor,
                          CancellationToken cancellationToken) => {
                              if (userAccessor.CurrentUser?.Id is null)
                                  return Results.Unauthorized();

                              var templates = await templateProvider.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
                              var depositTypes = await templateProvider.GetDepositTypesAsync(cancellationToken).ConfigureAwait(false);
                              var response = new StrongholdTemplatesResponse(
                           templates.Select(t => new StrongholdTemplateResponse(
                                                    t.Name,
                                                    t.Description,
                                                    t.RewardLevel,
                                                    t.Structures.Select(s => new StrongholdStructureResponse(
                                                                                    s.Type.ToDocumentValue(),
                                                                                    s.OffsetX,
                                                                                    s.OffsetY,
                                                                                    s.Level,
                                                                                    s.Behavior)).ToList()))
                                    .ToList(),
                           depositTypes);
                              return Results.Ok(response);
                          })
           .RequireTokenAuthentication()
           .WithName(TemplatesEndpointName);
    }

    private static void MapSpawn(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Stronghold.Spawn,
                    async ([FromBody] StrongholdSpawnRequest request,
                           IStrongholdControlService controlService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               if (userAccessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (!ValidateSpawnRequest(request, out var validationError))
                                   return Results.BadRequest(new ErrorResponse(validationError ?? InvalidParamsMessage));

                               if (!RoomReferenceParser.TryParse(request.Room, request.Shard, out var reference) || reference is null)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var options = new StrongholdSpawnOptions(request.Template,
                                                                 request.X,
                                                                 request.Y,
                                                                 request.OwnerUserId,
                                                                 request.DeployDelayTicks);
                               try {
                                   var result = await controlService.SpawnAsync(reference.RoomName, reference.ShardName, options, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new StrongholdSpawnResponse(result.RoomName, result.ShardName, result.TemplateName, result.InvaderCoreId));
                               }
                               catch (InvalidOperationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(SpawnEndpointName);
    }

    private static void MapExpand(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Stronghold.Expand,
                    async ([FromBody] StrongholdExpandRequest request,
                           IStrongholdControlService controlService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               if (userAccessor.CurrentUser?.Id is null)
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Room))
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               if (!RoomReferenceParser.TryParse(request.Room, request.Shard, out var reference) || reference is null)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var expanded = await controlService.ExpandAsync(reference.RoomName, reference.ShardName, cancellationToken).ConfigureAwait(false);
                               return expanded
                                   ? Results.Ok(new { ok = 1 })
                                   : Results.BadRequest(new ErrorResponse(StrongholdNotFoundMessage));
                           })
           .RequireTokenAuthentication()
           .WithName(ExpandEndpointName);
    }

    private static bool ValidateSpawnRequest(StrongholdSpawnRequest request, out string? error)
    {
        if (string.IsNullOrWhiteSpace(request.Room)) {
            error = "room is required";
            return false;
        }

        if (request.X.HasValue ^ request.Y.HasValue) {
            error = "both coordinates must be provided";
            return false;
        }

        if (request.X is < 0 or > 49) {
            error = "x must be between 0 and 49";
            return false;
        }

        if (request.Y is < 0 or > 49) {
            error = "y must be between 0 and 49";
            return false;
        }

        if (request.DeployDelayTicks is < 0) {
            error = "deploy delay must be non-negative";
            return false;
        }

        error = null;
        return true;
    }

    private sealed record StrongholdTemplatesResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("templates")] IReadOnlyCollection<StrongholdTemplateResponse> Templates,
        [property: JsonPropertyName("depositTypes")] IReadOnlyCollection<string> DepositTypes)
    {
        public StrongholdTemplatesResponse(IReadOnlyCollection<StrongholdTemplateResponse> templates, IReadOnlyCollection<string> depositTypes)
            : this(1, templates, depositTypes)
        {
        }
    }

    private sealed record StrongholdTemplateResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("rewardLevel")] int RewardLevel,
        [property: JsonPropertyName("structures")] IReadOnlyCollection<StrongholdStructureResponse> Structures);

    private sealed record StrongholdStructureResponse(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("dx")] int OffsetX,
        [property: JsonPropertyName("dy")] int OffsetY,
        [property: JsonPropertyName("level")] int? Level,
        [property: JsonPropertyName("behavior")] string? Behavior);

    private sealed record StrongholdSpawnRequest(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("template")] string? Template,
        [property: JsonPropertyName("x")] int? X,
        [property: JsonPropertyName("y")] int? Y,
        [property: JsonPropertyName("ownerUserId")] string? OwnerUserId,
        [property: JsonPropertyName("deployDelayTicks")] int? DeployDelayTicks);

    private sealed record StrongholdSpawnResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("template")] string Template,
        [property: JsonPropertyName("strongholdId")] string StrongholdId)
    {
        public StrongholdSpawnResponse(string room, string? shard, string template, string strongholdId)
            : this(1, room, shard, template, strongholdId)
        {
        }
    }

    private sealed record StrongholdExpandRequest(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard);
}
