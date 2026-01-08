namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class IntentEndpoints
{
    private const string AddObjectIntentEndpointName = "PostAddObjectIntent";
    private const string AddGlobalIntentEndpointName = "PostAddGlobalIntent";

    public static void Map(WebApplication app)
    {
        MapAddObjectIntent(app);
        MapAddGlobalIntent(app);
    }

    private static void MapAddObjectIntent(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Intent.AddObject,
                    async ([FromBody] AddObjectIntentRequest request,
                           IIntentService intentService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (!request.IsValid())
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   await intentService.AddObjectIntentAsync(request.Room, request.ObjectId, request.IntentName, request.IntentPayload, userId, cancellationToken).ConfigureAwait(false);
                               }
                               catch (IntentValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }

                               return Results.Ok(new { ok = 1 });
                           })
           .RequireTokenAuthentication()
           .WithName(AddObjectIntentEndpointName);
    }

    private static void MapAddGlobalIntent(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Intent.AddGlobal,
                    async ([FromBody] AddGlobalIntentRequest request,
                           IIntentService intentService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (!request.HasPayload)
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   await intentService.AddGlobalIntentAsync(request.IntentName, request.IntentPayload, userId, cancellationToken).ConfigureAwait(false);
                               }
                               catch (IntentValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }

                               return Results.Ok(new { ok = 1 });
                           })
           .RequireTokenAuthentication()
           .WithName(AddGlobalIntentEndpointName);
    }
}
