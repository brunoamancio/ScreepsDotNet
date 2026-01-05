namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Http;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Routing;

internal static class UserEndpoints
{
    private const string NotImplementedError = "NotImplemented";

    public static void Map(WebApplication app)
    {
        MapProtectedGet(app, ApiRoutes.User.WorldStartRoom, "GetUserWorldStartRoom");
        MapProtectedGet(app, ApiRoutes.User.WorldStatus, "GetUserWorldStatus");
        MapProtectedGet(app, ApiRoutes.User.Branches, "GetUserBranches");
        MapProtectedPost(app, ApiRoutes.User.Code, "PostUserCode");
        MapProtectedGet(app, ApiRoutes.User.Code, "GetUserCode");
        MapProtectedPost(app, ApiRoutes.User.Badge, "PostUserBadge");
        MapProtectedGet(app, ApiRoutes.User.RespawnProhibitedRooms, "GetUserRespawnProhibitedRooms");
        MapProtectedPost(app, ApiRoutes.User.Respawn, "PostUserRespawn");
        MapProtectedPost(app, ApiRoutes.User.SetActiveBranch, "PostUserSetActiveBranch");
        MapProtectedPost(app, ApiRoutes.User.CloneBranch, "PostUserCloneBranch");
        MapProtectedPost(app, ApiRoutes.User.DeleteBranch, "PostUserDeleteBranch");
        MapProtectedGet(app, ApiRoutes.User.Memory, "GetUserMemory");
        MapProtectedPost(app, ApiRoutes.User.Memory, "PostUserMemory");
        MapProtectedGet(app, ApiRoutes.User.MemorySegment, "GetUserMemorySegment");
        MapProtectedPost(app, ApiRoutes.User.MemorySegment, "PostUserMemorySegment");
        MapProtectedPost(app, ApiRoutes.User.Console, "PostUserConsole");
        MapProtectedPost(app, ApiRoutes.User.NotifyPrefs, "PostUserNotifyPrefs");
        MapProtectedGet(app, ApiRoutes.User.Overview, "GetUserOverview");
        MapProtectedPost(app, ApiRoutes.User.TutorialDone, "PostUserTutorialDone");
        MapProtectedPost(app, ApiRoutes.User.Email, "PostUserEmail");
        MapProtectedGet(app, ApiRoutes.User.MoneyHistory, "GetUserMoneyHistory");
        MapProtectedPost(app, ApiRoutes.User.SetSteamVisible, "PostUserSetSteamVisible");

        MapPublicGet(app, ApiRoutes.User.Find, "GetUserFind");
        MapPublicGet(app, ApiRoutes.User.Stats, "GetUserStats");
        MapPublicGet(app, ApiRoutes.User.Rooms, "GetUserRooms");
        MapPublicGet(app, ApiRoutes.User.BadgeSvg, "GetUserBadgeSvg");
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
}
