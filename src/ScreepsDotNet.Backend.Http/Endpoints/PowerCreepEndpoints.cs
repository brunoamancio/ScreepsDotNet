namespace ScreepsDotNet.Backend.Http.Endpoints;

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class PowerCreepEndpoints
{
    private const string ListEndpointName = "GetPowerCreeps";
    private const string CreateEndpointName = "PostCreatePowerCreep";
    private const string DeleteEndpointName = "PostDeletePowerCreep";
    private const string CancelDeleteEndpointName = "PostCancelDeletePowerCreep";
    private const string UpgradeEndpointName = "PostUpgradePowerCreep";
    private const string RenameEndpointName = "PostRenamePowerCreep";
    private const string ExperimentationEndpointName = "PostPowerCreepExperimentation";

    public static void Map(WebApplication app)
    {
        MapList(app);
        MapCreate(app);
        MapDelete(app);
        MapCancelDelete(app);
        MapUpgrade(app);
        MapRename(app);
        MapExperimentation(app);
    }

    private static void MapList(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.PowerCreeps.List,
                   async (ICurrentUserAccessor userAccessor,
                          IPowerCreepService powerCreepService,
                          CancellationToken cancellationToken) => {
                              var userId = userAccessor.CurrentUser?.Id;
                              if (string.IsNullOrEmpty(userId))
                                  return Results.Unauthorized();

                              var creeps = await powerCreepService.GetListAsync(userId, cancellationToken).ConfigureAwait(false);
                              var response = new PowerCreepListResponse([.. creeps.Select(PowerCreepResponse.FromModel)]);
                              return Results.Ok(response);
                          })
           .RequireTokenAuthentication()
           .WithName(ListEndpointName);
    }

    private static void MapCreate(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.PowerCreeps.Create,
                    async ([FromBody] PowerCreepCreateRequest request,
                           ICurrentUserAccessor userAccessor,
                           IPowerCreepService powerCreepService,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ClassName))
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   var creep = await powerCreepService.CreateAsync(userId, request.Name, request.ClassName, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new PowerCreepMutationResponse(PowerCreepResponse.FromModel(creep)));
                               }
                               catch (PowerCreepValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(CreateEndpointName);
    }

    private static void MapDelete(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.PowerCreeps.Delete,
                    async ([FromBody] PowerCreepIdRequest request,
                           ICurrentUserAccessor userAccessor,
                           IPowerCreepService powerCreepService,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Id))
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   await powerCreepService.DeleteAsync(userId, request.Id, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (PowerCreepValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(DeleteEndpointName);
    }

    private static void MapCancelDelete(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.PowerCreeps.CancelDelete,
                    async ([FromBody] PowerCreepIdRequest request,
                           ICurrentUserAccessor userAccessor,
                           IPowerCreepService powerCreepService,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Id))
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   await powerCreepService.CancelDeleteAsync(userId, request.Id, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (PowerCreepValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(CancelDeleteEndpointName);
    }

    private static void MapUpgrade(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.PowerCreeps.Upgrade,
                    async ([FromBody] PowerCreepUpgradeRequest request,
                           ICurrentUserAccessor userAccessor,
                           IPowerCreepService powerCreepService,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Id) || request.Powers is null)
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   await powerCreepService.UpgradeAsync(userId, request.Id, request.Powers, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (PowerCreepValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(UpgradeEndpointName);
    }

    private static void MapRename(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.PowerCreeps.Rename,
                    async ([FromBody] PowerCreepRenameRequest request,
                           ICurrentUserAccessor userAccessor,
                           IPowerCreepService powerCreepService,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name))
                                   return Results.BadRequest(new ErrorResponse("invalid params"));

                               try {
                                   await powerCreepService.RenameAsync(userId, request.Id, request.Name, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (PowerCreepValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(RenameEndpointName);
    }

    private static void MapExperimentation(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.PowerCreeps.Experimentation,
                    async (ICurrentUserAccessor userAccessor,
                           IPowerCreepService powerCreepService,
                           CancellationToken cancellationToken) => {
                               var userId = userAccessor.CurrentUser?.Id;
                               if (string.IsNullOrEmpty(userId))
                                   return Results.Unauthorized();

                               try {
                                   await powerCreepService.RegisterExperimentationAsync(userId, cancellationToken).ConfigureAwait(false);
                                   return Results.Ok(new { ok = 1 });
                               }
                               catch (PowerCreepValidationException ex) {
                                   return Results.BadRequest(new ErrorResponse(ex.Message));
                               }
                           })
           .RequireTokenAuthentication()
           .WithName(ExperimentationEndpointName);
    }

    private sealed record PowerCreepCreateRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("className")] string ClassName);

    private sealed record PowerCreepIdRequest(
        [property: JsonPropertyName("id")] string Id);

    private sealed record PowerCreepUpgradeRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("powers")] Dictionary<string, int>? Powers);

    private sealed record PowerCreepRenameRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record PowerCreepListResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("list")] IReadOnlyCollection<PowerCreepResponse> List)
    {
        public PowerCreepListResponse(IReadOnlyCollection<PowerCreepResponse> list)
            : this(1, list)
        {
        }
    }

    private sealed record PowerCreepMutationResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("creep")] PowerCreepResponse Creep)
    {
        public PowerCreepMutationResponse(PowerCreepResponse creep)
            : this(1, creep)
        {
        }
    }

    private sealed record PowerCreepResponse(
        [property: JsonPropertyName("_id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("className")] string ClassName,
        [property: JsonPropertyName("level")] int Level,
        [property: JsonPropertyName("hitsMax")] int HitsMax,
        [property: JsonPropertyName("store")] IReadOnlyDictionary<string, int> Store,
        [property: JsonPropertyName("storeCapacity")] int StoreCapacity,
        [property: JsonPropertyName("spawnCooldownTime")] long? SpawnCooldownTime,
        [property: JsonPropertyName("deleteTime")] long? DeleteTime,
        [property: JsonPropertyName("shard")] string? Shard,
        [property: JsonPropertyName("powers")] IReadOnlyDictionary<string, int> Powers,
        [property: JsonPropertyName("room")] string? Room,
        [property: JsonPropertyName("x")] int? X,
        [property: JsonPropertyName("y")] int? Y,
        [property: JsonPropertyName("hits")] int? Hits,
        [property: JsonPropertyName("fatigue")] int? Fatigue,
        [property: JsonPropertyName("ticksToLive")] int? TicksToLive)
    {
        public static PowerCreepResponse FromModel(PowerCreepListItem listItem)
            => new(
                listItem.Id,
                listItem.Name,
                listItem.ClassName,
                listItem.Level,
                listItem.HitsMax,
                listItem.Store,
                listItem.StoreCapacity,
                listItem.SpawnCooldownTime,
                listItem.DeleteTime,
                listItem.Shard,
                listItem.Powers,
                listItem.Room,
                listItem.X,
                listItem.Y,
                listItem.Hits,
                listItem.Fatigue,
                listItem.TicksToLive);
    }
}
