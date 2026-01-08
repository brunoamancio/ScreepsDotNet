namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class BotEndpoints
{
    private const string ListBotsEndpointName = "GetBotDefinitions";
    private const string SpawnBotEndpointName = "PostBotSpawn";
    private const string ReloadBotEndpointName = "PostBotReload";
    private const string RemoveBotEndpointName = "PostBotRemove";

    public static void Map(WebApplication app)
    {
        MapListBots(app);
        MapSpawnBot(app);
        MapReloadBot(app);
        MapRemoveBot(app);
    }

    private static void MapListBots(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Bot.List,
                   async (IBotDefinitionProvider definitionProvider,
                          CancellationToken cancellationToken) =>
                   {
                       var definitions = await definitionProvider.GetDefinitionsAsync(cancellationToken).ConfigureAwait(false);
                       var response = new BotListResponse(
                           definitions.Select(def => new BotDefinitionResponse(def.Name, def.Description, def.Modules.Keys)).ToList());
                       return Results.Ok(response);
                   })
           .RequireTokenAuthentication()
           .WithName(ListBotsEndpointName);
    }

    private static void MapSpawnBot(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Bot.Spawn,
                    async ([FromBody] BotSpawnRequest request,
                           IBotControlService botControlService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (userAccessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        if (string.IsNullOrWhiteSpace(request.Bot) || string.IsNullOrWhiteSpace(request.Room))
                            return Results.BadRequest(new ErrorResponse("invalid params"));

                        try
                        {
                            var options = new BotSpawnOptions(request.Username, request.Cpu, request.GlobalControlLevel, request.SpawnX, request.SpawnY);
                            var result = await botControlService.SpawnAsync(request.Bot, request.Room, options, cancellationToken).ConfigureAwait(false);
                            return Results.Ok(new
                            {
                                ok = 1,
                                userId = result.UserId,
                                username = result.Username,
                                room = result.RoomName,
                                spawn = new { x = result.SpawnX, y = result.SpawnY }
                            });
                        }
                        catch (InvalidOperationException ex)
                        {
                            return Results.BadRequest(new ErrorResponse(ex.Message));
                        }
                    })
           .RequireTokenAuthentication()
           .WithName(SpawnBotEndpointName);
    }

    private static void MapReloadBot(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Bot.Reload,
                    async ([FromBody] BotReloadRequest request,
                           IBotControlService botControlService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (userAccessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        if (string.IsNullOrWhiteSpace(request.Bot))
                            return Results.BadRequest(new ErrorResponse("invalid params"));

                        try
                        {
                            var reloaded = await botControlService.ReloadAsync(request.Bot, cancellationToken).ConfigureAwait(false);
                            return Results.Ok(new { ok = 1, usersReloaded = reloaded });
                        }
                        catch (InvalidOperationException ex)
                        {
                            return Results.BadRequest(new ErrorResponse(ex.Message));
                        }
                    })
           .RequireTokenAuthentication()
           .WithName(ReloadBotEndpointName);
    }

    private static void MapRemoveBot(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.Bot.Remove,
                    async ([FromBody] BotRemoveRequest request,
                           IBotControlService botControlService,
                           ICurrentUserAccessor userAccessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (userAccessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        if (string.IsNullOrWhiteSpace(request.Username))
                            return Results.BadRequest(new ErrorResponse("invalid params"));

                        try
                        {
                            var removed = await botControlService.RemoveAsync(request.Username, cancellationToken).ConfigureAwait(false);
                            if (!removed)
                                return Results.BadRequest(new ErrorResponse("user not found"));

                            return Results.Ok(new { ok = 1 });
                        }
                        catch (InvalidOperationException ex)
                        {
                            return Results.BadRequest(new ErrorResponse(ex.Message));
                        }
                    })
           .RequireTokenAuthentication()
           .WithName(RemoveBotEndpointName);
    }

    private sealed record BotListResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("bots")] IReadOnlyCollection<BotDefinitionResponse> Bots)
    {
        public BotListResponse(IReadOnlyCollection<BotDefinitionResponse> bots)
            : this(1, bots)
        {
        }
    }

    private sealed record BotDefinitionResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("modules")] IEnumerable<string> Modules);

    private sealed record BotSpawnRequest(
        [property: JsonPropertyName("bot")] string Bot,
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("cpu")] int? Cpu,
        [property: JsonPropertyName("gcl")] int? GlobalControlLevel,
        [property: JsonPropertyName("x")] int? SpawnX,
        [property: JsonPropertyName("y")] int? SpawnY);

    private sealed record BotReloadRequest(
        [property: JsonPropertyName("bot")] string Bot);

    private sealed record BotRemoveRequest(
        [property: JsonPropertyName("username")] string Username);
}
