namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Helpers;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class ObjectNameEndpoints
{
    private const string InvalidParamsMessage = "invalid params";
    private const string NameExistsMessage = "name exists";
    private const string GenerateEndpointName = "PostGenerateUniqueObjectName";
    private const string CheckEndpointName = "PostCheckUniqueObjectName";
    private const string SpawnType = "spawn";

    public static void Map(WebApplication app)
    {
        MapGenerate(app);
        MapCheck(app);
    }

    private static void MapGenerate(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.ObjectNames.GenerateUnique,
                    async ([FromBody] UniqueObjectNameRequest request,
                           ICurrentUserAccessor accessor,
                           IObjectNameService objectNameService,
                           CancellationToken cancellationToken) => {
                               if (!string.Equals(request.Type, SpawnType, StringComparison.OrdinalIgnoreCase))
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var user = UserEndpointGuards.RequireUser(accessor, "User context is not available.");
                               var name = await objectNameService.GenerateSpawnNameAsync(user.Id, cancellationToken).ConfigureAwait(false);
                               return Results.Ok(new { name });
                           })
           .RequireTokenAuthentication()
           .WithName(GenerateEndpointName);
    }

    private static void MapCheck(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.ObjectNames.CheckUnique,
                    async ([FromBody] CheckUniqueObjectNameRequest request,
                           ICurrentUserAccessor accessor,
                           IObjectNameService objectNameService,
                           CancellationToken cancellationToken) => {
                               if (!string.Equals(request.Type, SpawnType, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.Name))
                                   return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                               var user = UserEndpointGuards.RequireUser(accessor, "User context is not available.");
                               var unique = await objectNameService.IsSpawnNameUniqueAsync(user.Id, request.Name, cancellationToken).ConfigureAwait(false);
                               return unique
                                   ? Results.Ok(UserResponseFactory.CreateEmpty())
                                   : Results.BadRequest(new ErrorResponse(NameExistsMessage));
                           })
           .RequireTokenAuthentication()
           .WithName(CheckEndpointName);
    }

    private sealed record UniqueObjectNameRequest(string? Type);

    private sealed record CheckUniqueObjectNameRequest(string? Type, string? Name);
}
