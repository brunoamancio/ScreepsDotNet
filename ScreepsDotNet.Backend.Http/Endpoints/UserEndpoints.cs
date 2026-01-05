namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Http;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Routing;

internal static class UserEndpoints
{
    private const string NotImplementedError = "NotImplemented";
    private const string MissingUserContextMessage = "User context is not available.";

    private const string DefaultWorldStartRoom = "W5N5";

    private const string WorldStartRoomEndpointName = "GetUserWorldStartRoom";
    private const string WorldStatusEndpointName = "GetUserWorldStatus";
    private const string BranchesEndpointName = "GetUserBranches";
    private const string PostCodeEndpointName = "PostUserCode";
    private const string GetCodeEndpointName = "GetUserCode";
    private const string BadgeEndpointName = "PostUserBadge";
    private const string RespawnProhibitedRoomsEndpointName = "GetUserRespawnProhibitedRooms";
    private const string RespawnEndpointName = "PostUserRespawn";
    private const string SetActiveBranchEndpointName = "PostUserSetActiveBranch";
    private const string CloneBranchEndpointName = "PostUserCloneBranch";
    private const string DeleteBranchEndpointName = "PostUserDeleteBranch";
    private const string GetMemoryEndpointName = "GetUserMemory";
    private const string PostMemoryEndpointName = "PostUserMemory";
    private const string GetMemorySegmentEndpointName = "GetUserMemorySegment";
    private const string PostMemorySegmentEndpointName = "PostUserMemorySegment";
    private const string ConsoleEndpointName = "PostUserConsole";
    private const string NotifyPrefsEndpointName = "PostUserNotifyPrefs";
    private const string OverviewEndpointName = "GetUserOverview";
    private const string TutorialDoneEndpointName = "PostUserTutorialDone";
    private const string EmailEndpointName = "PostUserEmail";
    private const string MoneyHistoryEndpointName = "GetUserMoneyHistory";
    private const string SetSteamVisibleEndpointName = "PostUserSetSteamVisible";
    private const string FindEndpointName = "GetUserFind";
    private const string StatsEndpointName = "GetUserStats";
    private const string RoomsEndpointName = "GetUserRooms";
    private const string BadgeSvgEndpointName = "GetUserBadgeSvg";

    public static void Map(WebApplication app)
    {
        MapProtectedWorldStartRoom(app);
        MapProtectedWorldStatus(app);
        MapProtectedGet(app, ApiRoutes.User.Branches, BranchesEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Code, PostCodeEndpointName);
        MapProtectedGet(app, ApiRoutes.User.Code, GetCodeEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Badge, BadgeEndpointName);
        MapProtectedGet(app, ApiRoutes.User.RespawnProhibitedRooms, RespawnProhibitedRoomsEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Respawn, RespawnEndpointName);
        MapProtectedPost(app, ApiRoutes.User.SetActiveBranch, SetActiveBranchEndpointName);
        MapProtectedPost(app, ApiRoutes.User.CloneBranch, CloneBranchEndpointName);
        MapProtectedPost(app, ApiRoutes.User.DeleteBranch, DeleteBranchEndpointName);
        MapProtectedGet(app, ApiRoutes.User.Memory, GetMemoryEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Memory, PostMemoryEndpointName);
        MapProtectedGet(app, ApiRoutes.User.MemorySegment, GetMemorySegmentEndpointName);
        MapProtectedPost(app, ApiRoutes.User.MemorySegment, PostMemorySegmentEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Console, ConsoleEndpointName);
        MapProtectedPost(app, ApiRoutes.User.NotifyPrefs, NotifyPrefsEndpointName);
        MapProtectedGet(app, ApiRoutes.User.Overview, OverviewEndpointName);
        MapProtectedPost(app, ApiRoutes.User.TutorialDone, TutorialDoneEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Email, EmailEndpointName);
        MapProtectedGet(app, ApiRoutes.User.MoneyHistory, MoneyHistoryEndpointName);
        MapProtectedPost(app, ApiRoutes.User.SetSteamVisible, SetSteamVisibleEndpointName);

        MapPublicGet(app, ApiRoutes.User.Find, FindEndpointName);
        MapPublicGet(app, ApiRoutes.User.Stats, StatsEndpointName);
        MapPublicGet(app, ApiRoutes.User.Rooms, RoomsEndpointName);
        MapPublicGet(app, ApiRoutes.User.BadgeSvg, BadgeSvgEndpointName);
    }

    private static void MapProtectedWorldStartRoom(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.WorldStartRoom,
                   async (ICurrentUserAccessor userAccessor, IUserWorldRepository repository, CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       var room = await repository.GetRandomControllerRoomAsync(user.Id, cancellationToken).ConfigureAwait(false)
                                  ?? DefaultWorldStartRoom;
                       return Results.Ok(new { room = new[] { room } });
                   })
           .RequireTokenAuthentication()
           .WithName(WorldStartRoomEndpointName);
    }

    private static void MapProtectedWorldStatus(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.WorldStatus,
                   async (ICurrentUserAccessor userAccessor, IUserWorldRepository repository, CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       var status = await repository.GetWorldStatusAsync(user.Id, cancellationToken).ConfigureAwait(false);
                       return Results.Ok(new { status = status.ToString().ToLowerInvariant() });
                   })
           .RequireTokenAuthentication()
           .WithName(WorldStatusEndpointName);
    }

    private static void MapProtectedGet(WebApplication app, string route, string name)
        => app.MapGet(route, () => NotImplemented(route))
              .RequireTokenAuthentication()
              .WithName(name);

    private static void MapProtectedPost(WebApplication app, string route, string name)
        => app.MapPost(route, () => NotImplemented(route))
              .RequireTokenAuthentication()
              .WithName(name);

    private static void MapPublicGet(WebApplication app, string route, string name)
        => app.MapGet(route, () => NotImplemented(route))
              .WithName(name);

    private static IResult NotImplemented(string route)
        => Results.Json(new { error = NotImplementedError, route }, statusCode: StatusCodes.Status501NotImplemented);

    private static UserProfile RequireUser(ICurrentUserAccessor accessor)
        => accessor.CurrentUser ?? throw new InvalidOperationException(MissingUserContextMessage);
}
