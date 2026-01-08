namespace ScreepsDotNet.Backend.Http.Endpoints;

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class SystemEndpoints
{
    private const string StatusEndpointName = "GetSystemStatus";
    private const string PauseEndpointName = "PostSystemPause";
    private const string ResumeEndpointName = "PostSystemResume";
    private const string TickGetEndpointName = "GetSystemTickDuration";
    private const string TickSetEndpointName = "PostSystemTickDuration";
    private const string MessageEndpointName = "PostSystemMessage";

    public static void Map(WebApplication app)
    {
        MapStatus(app);
        MapPause(app);
        MapResume(app);
        MapTickGet(app);
        MapTickSet(app);
        MapMessage(app);
    }

    private static void MapStatus(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.System.Status,
                   async (ISystemControlService controlService,
                          ICurrentUserAccessor accessor,
                          CancellationToken cancellationToken) =>
                   {
                       if (accessor.CurrentUser?.Id is null)
                           return Results.Unauthorized();

                       var paused = await controlService.IsSimulationPausedAsync(cancellationToken).ConfigureAwait(false);
                       var duration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);
                       return Results.Ok(new SystemStatusResponse(paused, duration));
                   })
           .RequireTokenAuthentication()
           .WithName(StatusEndpointName);
    }

    private static void MapPause(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.System.Pause,
                    async (ISystemControlService controlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (accessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        await controlService.PauseSimulationAsync(cancellationToken).ConfigureAwait(false);
                        return Results.Ok(new { ok = 1 });
                    })
           .RequireTokenAuthentication()
           .WithName(PauseEndpointName);
    }

    private static void MapResume(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.System.Resume,
                    async (ISystemControlService controlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (accessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        await controlService.ResumeSimulationAsync(cancellationToken).ConfigureAwait(false);
                        return Results.Ok(new { ok = 1 });
                    })
           .RequireTokenAuthentication()
           .WithName(ResumeEndpointName);
    }

    private static void MapTickGet(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.System.Tick,
                   async (ISystemControlService controlService,
                          ICurrentUserAccessor accessor,
                          CancellationToken cancellationToken) =>
                   {
                       if (accessor.CurrentUser?.Id is null)
                           return Results.Unauthorized();

                       var duration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);
                       return Results.Ok(new TickDurationResponse(duration));
                   })
           .RequireTokenAuthentication()
           .WithName(TickGetEndpointName);
    }

    private static void MapTickSet(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.System.TickSet,
                    async ([FromBody] TickDurationRequest request,
                           ISystemControlService controlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (accessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        if (request.DurationMilliseconds <= 0)
                            return Results.BadRequest(new ErrorResponse("duration must be positive"));

                        await controlService.SetTickDurationAsync(request.DurationMilliseconds, cancellationToken).ConfigureAwait(false);
                        return Results.Ok(new { ok = 1 });
                    })
           .RequireTokenAuthentication()
           .WithName(TickSetEndpointName);
    }

    private static void MapMessage(WebApplication app)
    {
        app.MapPost(ApiRoutes.Game.System.Message,
                    async ([FromBody] SystemMessageRequest request,
                           ISystemControlService controlService,
                           ICurrentUserAccessor accessor,
                           CancellationToken cancellationToken) =>
                    {
                        if (accessor.CurrentUser?.Id is null)
                            return Results.Unauthorized();

                        if (string.IsNullOrWhiteSpace(request.Message))
                            return Results.BadRequest(new ErrorResponse("message is required"));

                        try
                        {
                            await controlService.PublishServerMessageAsync(request.Message, cancellationToken).ConfigureAwait(false);
                            return Results.Ok(new { ok = 1 });
                        }
                        catch (ArgumentException ex)
                        {
                            return Results.BadRequest(new ErrorResponse(ex.Message));
                        }
                    })
           .RequireTokenAuthentication()
           .WithName(MessageEndpointName);
    }

    private sealed record SystemStatusResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("paused")] bool Paused,
        [property: JsonPropertyName("tickDuration")] int? TickDurationMilliseconds)
    {
        public SystemStatusResponse(bool paused, int? tickDurationMilliseconds)
            : this(1, paused, tickDurationMilliseconds)
        {
        }
    }

    private sealed record TickDurationResponse(
        [property: JsonPropertyName("ok")] int Ok,
        [property: JsonPropertyName("duration")] int? DurationMilliseconds)
    {
        public TickDurationResponse(int? durationMilliseconds)
            : this(1, durationMilliseconds)
        {
        }
    }

    private sealed record TickDurationRequest(
        [property: JsonPropertyName("duration")] int DurationMilliseconds);

    private sealed record SystemMessageRequest(
        [property: JsonPropertyName("message")] string Message);
}
