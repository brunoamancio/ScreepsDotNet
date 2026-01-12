namespace ScreepsDotNet.Backend.Http.Endpoints;

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class FlagEndpoints
{
    private const string InvalidParamsMessage = "invalid params";
    private const string CreateFlagEndpointName = "PostCreateFlag";
    private const string ChangeFlagColorEndpointName = "PostChangeFlagColor";
    private const string RemoveFlagEndpointName = "PostRemoveFlag";

    public static void Map(WebApplication app)
    {
        MapCreateFlag(app);
        MapChangeFlagColor(app);
        MapRemoveFlag(app);
    }

    private static void MapCreateFlag(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.CreateFlag,
                    async ([FromBody] CreateFlagRequestModel request,
                           IFlagService flagService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (!RoomReferenceParser.TryParse(request.Room, request.Shard, out var reference) || reference is null)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var createRequest = new CreateFlagRequest(reference.RoomName,
                                                                          request.X,
                                                                          request.Y,
                                                                          request.Name,
                                                                          request.Color,
                                                                          request.SecondaryColor,
                                                                          reference.ShardName);

                               var result = await flagService.CreateFlagAsync(userId, createRequest, cancellationToken).ConfigureAwait(false);

                               return MapResult(result);
                           })
           .RequireTokenAuthentication()
           .WithName(CreateFlagEndpointName);
    }

    private static void MapChangeFlagColor(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.ChangeFlagColor,
                    async ([FromBody] ChangeFlagColorRequestModel request,
                           IFlagService flagService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (!RoomReferenceParser.TryParse(request.Room, request.Shard, out var reference) || reference is null)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var result = await flagService.ChangeFlagColorAsync(userId,
                                                                      reference.RoomName,
                                                                      reference.ShardName,
                                                                      request.Name,
                                                                      request.Color,
                                                                      request.SecondaryColor,
                                                                      cancellationToken).ConfigureAwait(false);

                               return MapResult(result);
                           })
           .RequireTokenAuthentication()
           .WithName(ChangeFlagColorEndpointName);
    }

    private static void MapRemoveFlag(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.RemoveFlag,
                    async ([FromBody] RemoveFlagRequestModel request,
                           IFlagService flagService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (!RoomReferenceParser.TryParse(request.Room, request.Shard, out var reference) || reference is null)
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var result = await flagService.RemoveFlagAsync(userId,
                                                                              reference.RoomName,
                                                                              reference.ShardName,
                                                                              request.Name,
                                                                              cancellationToken).ConfigureAwait(false);

                               return MapResult(result);
                           })
           .RequireTokenAuthentication()
           .WithName(RemoveFlagEndpointName);
    }

    private static IResult MapResult(FlagResult result)
    {
        return result.Status switch
        {
            FlagResultStatus.Success => Results.Ok(new { ok = 1 }),
            FlagResultStatus.InvalidParams => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid params")),
            FlagResultStatus.TooManyFlags => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "too many")),
            FlagResultStatus.FlagNotFound => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "not found")),
            _ => Results.BadRequest(new ErrorResponse("unknown error"))
        };
    }

    internal sealed record CreateFlagRequestModel(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("x")] int X,
        [property: JsonPropertyName("y")] int Y,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("color")] Color Color,
        [property: JsonPropertyName("secondaryColor")] Color SecondaryColor);

    internal sealed record ChangeFlagColorRequestModel(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("color")] Color Color,
        [property: JsonPropertyName("secondaryColor")] Color SecondaryColor);

    internal sealed record RemoveFlagRequestModel(
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("name")] string Name);
}
