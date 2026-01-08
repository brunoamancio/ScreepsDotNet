namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class SpawnEndpoints
{
    private const string PlaceSpawnEndpointName = "PostPlaceSpawn";

    public static void Map(WebApplication app)
        => MapPlaceSpawn(app);

    private static void MapPlaceSpawn(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.PlaceSpawn,
                    async ([FromBody] PlaceSpawnRequest request,
                           IPlayerSpawnService spawnService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               var result = await spawnService.PlaceSpawnAsync(userId, request, cancellationToken).ConfigureAwait(false);

                               return result.Status switch
                               {
                                   PlaceSpawnResultStatus.Success => Results.Ok(new { ok = 1 }),
                                   PlaceSpawnResultStatus.InvalidParams => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid params")),
                                   PlaceSpawnResultStatus.Blocked => Results.BadRequest(new ErrorResponse("blocked")),
                                   PlaceSpawnResultStatus.NoCpu => Results.BadRequest(new ErrorResponse("no cpu")),
                                   PlaceSpawnResultStatus.TooSoonAfterLastRespawn => Results.BadRequest(new ErrorResponse("too soon after last respawn")),
                                   PlaceSpawnResultStatus.AlreadyPlaying => Results.BadRequest(new ErrorResponse("already playing")),
                                   PlaceSpawnResultStatus.InvalidRoom => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid room")),
                                   PlaceSpawnResultStatus.InvalidPosition => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid position")),
                                   PlaceSpawnResultStatus.UserNotFound => Results.Unauthorized(),
                                   _ => Results.BadRequest(new ErrorResponse("unknown error"))
                               };
                           })
           .RequireTokenAuthentication()
           .WithName(PlaceSpawnEndpointName);
    }
}
