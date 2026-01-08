namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class InvaderEndpoints
{
    private const string CreateInvaderEndpointName = "PostCreateInvader";
    private const string RemoveInvaderEndpointName = "PostRemoveInvader";

    public static void Map(WebApplication app)
    {
        MapCreateInvader(app);
        MapRemoveInvader(app);
    }

    private static void MapCreateInvader(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.CreateInvader,
                    async ([FromBody] CreateInvaderRequest request,
                           IInvaderService invaderService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               var result = await invaderService.CreateInvaderAsync(userId, request, cancellationToken).ConfigureAwait(false);

                               return result.Status switch
                               {
                                   CreateInvaderResultStatus.Success => Results.Ok(new { ok = 1, _id = result.Id }),
                                   CreateInvaderResultStatus.InvalidParams => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid params")),
                                   CreateInvaderResultStatus.TooManyInvaders => Results.BadRequest(new ErrorResponse("too many invaders exist")),
                                   CreateInvaderResultStatus.HostilesPresent => Results.BadRequest(new ErrorResponse("hostiles present")),
                                   CreateInvaderResultStatus.NotOwned => Results.BadRequest(new ErrorResponse("not owned")),
                                   CreateInvaderResultStatus.InvalidRoom => Results.BadRequest(new ErrorResponse(result.ErrorMessage ?? "invalid room")),
                                   CreateInvaderResultStatus.UserNotFound => Results.Unauthorized(),
                                   _ => Results.BadRequest(new ErrorResponse("unknown error"))
                               };
                           })
           .RequireTokenAuthentication()
           .WithName(CreateInvaderEndpointName);
    }

    private static void MapRemoveInvader(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.World.RemoveInvader,
                    async ([FromBody] RemoveInvaderRequest request,
                           IInvaderService invaderService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               var result = await invaderService.RemoveInvaderAsync(userId, request, cancellationToken).ConfigureAwait(false);

                               return result.Status switch
                               {
                                   RemoveInvaderResultStatus.Success => Results.Ok(new { ok = 1 }),
                                   RemoveInvaderResultStatus.InvalidObject => Results.BadRequest(new ErrorResponse("invalid object")),
                                   RemoveInvaderResultStatus.UserNotFound => Results.Unauthorized(),
                                   _ => Results.BadRequest(new ErrorResponse("unknown error"))
                               };
                           })
           .RequireTokenAuthentication()
           .WithName(RemoveInvaderEndpointName);
    }
}
