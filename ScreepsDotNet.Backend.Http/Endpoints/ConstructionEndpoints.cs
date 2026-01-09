namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class ConstructionEndpoints
{
    private const string CreateConstructionEndpointName = "PostCreateConstruction";

    public static void Map(WebApplication app)
        => MapCreateConstruction(app);

    private static void MapCreateConstruction(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.CreateConstruction,
                    async ([FromBody] PlaceConstructionRequest request,
                           IConstructionService constructionService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (!RoomReferenceParser.TryParse(request.Room, request.Shard, out var roomReference) || roomReference is null)
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               var normalizedRequest = request with
                               {
                                   Room = roomReference.RoomName,
                                   Shard = roomReference.ShardName
                               };

                               var result = await constructionService.CreateConstructionAsync(userId, normalizedRequest, cancellationToken).ConfigureAwait(false);

                               return result.Status switch
                               {
                                   PlaceConstructionResultStatus.Success => Results.Ok(new { ok = 1, _id = result.Id }),
                                   PlaceConstructionResultStatus.InvalidParams => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid params")),
                                   PlaceConstructionResultStatus.InvalidLocation => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid location")),
                                   PlaceConstructionResultStatus.NotControllerOwner => Results.BadRequest(new ErrorResponse("not a controller owner")),
                                   PlaceConstructionResultStatus.RclNotEnough => Results.BadRequest(new ErrorResponse("RCL not enough")),
                                   PlaceConstructionResultStatus.TooMany => Results.BadRequest(new ErrorResponse("too many")),
                                   PlaceConstructionResultStatus.InvalidRoom => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid room")),
                                   PlaceConstructionResultStatus.UserNotFound => Results.Unauthorized(),
                                   _ => Results.BadRequest(new ErrorResponse("unknown error"))
                               };
                           })
           .RequireTokenAuthentication()
           .WithName(CreateConstructionEndpointName);
    }
}
