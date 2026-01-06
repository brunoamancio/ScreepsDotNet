namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Constants;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class UserEndpoints
{
    private const string UsernameQueryName = "username";
    private const string BorderQueryName = "border";
    private const string BorderEnabledValue = "1";
    private const string BorderEnabledAlternateValue = "true";
    private const string UserIdQueryName = "id";
    private const string NotImplementedError = "NotImplemented";
    private const string MissingUserContextMessage = "User context is not available.";
    private const string MissingUsernameMessage = "username is required.";
    private const string MissingUserIdentifierMessage = "username or id must be provided.";
    private const string MissingUserIdMessage = "user id is required.";
    private const string UserNotFoundMessage = "user not found";
    private const string InvalidStatsIntervalMessage = "invalid params";
    private const string NoCodeMessage = "no code";

    private const string DefaultWorldStartRoom = "W5N5";

    private const string ActiveWorldIdentifier = "$activeWorld";
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
    private static readonly int[] AllowedStatsIntervals = [8, 180, 1440];
    private static readonly int[] AllowedNotifyIntervals = [5, 10, 30, 60, 180, 360, 720, 1440, 4320];
    private static readonly int[] AllowedNotifyErrorsIntervals = [0, 5, 10, 30, 60, 180, 360, 720, 1440, 4320, 100000];

    public static void Map(WebApplication app)
    {
        MapProtectedWorldStartRoom(app);
        MapProtectedWorldStatus(app);
        MapProtectedBranches(app);
        MapProtectedPost(app, ApiRoutes.User.Code, PostCodeEndpointName);
        MapProtectedCode(app);
        MapProtectedPost(app, ApiRoutes.User.Badge, BadgeEndpointName);
        MapProtectedRespawnProhibitedRooms(app);
        MapProtectedPost(app, ApiRoutes.User.Respawn, RespawnEndpointName);
        MapProtectedPost(app, ApiRoutes.User.SetActiveBranch, SetActiveBranchEndpointName);
        MapProtectedPost(app, ApiRoutes.User.CloneBranch, CloneBranchEndpointName);
        MapProtectedPost(app, ApiRoutes.User.DeleteBranch, DeleteBranchEndpointName);
        MapProtectedGet(app, ApiRoutes.User.Memory, GetMemoryEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Memory, PostMemoryEndpointName);
        MapProtectedGet(app, ApiRoutes.User.MemorySegment, GetMemorySegmentEndpointName);
        MapProtectedPost(app, ApiRoutes.User.MemorySegment, PostMemorySegmentEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Console, ConsoleEndpointName);
        MapProtectedNotifyPrefs(app);
        MapProtectedGet(app, ApiRoutes.User.Overview, OverviewEndpointName);
        MapProtectedPost(app, ApiRoutes.User.TutorialDone, TutorialDoneEndpointName);
        MapProtectedPost(app, ApiRoutes.User.Email, EmailEndpointName);
        MapProtectedGet(app, ApiRoutes.User.MoneyHistory, MoneyHistoryEndpointName);
        MapProtectedPost(app, ApiRoutes.User.SetSteamVisible, SetSteamVisibleEndpointName);

        MapPublicFind(app);
        MapPublicRooms(app);
        MapPublicBadgeSvg(app);
        MapPublicStats(app);
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

    private static void MapProtectedBranches(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Branches,
                   async (ICurrentUserAccessor userAccessor,
                          IUserCodeRepository repository,
                          CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       var branches = await repository.GetBranchesAsync(user.Id, cancellationToken).ConfigureAwait(false);

                       var payload = branches.Select(branch => new Dictionary<string, object?>
                       {
                           [UserResponseFields.Branch] = branch.Branch,
                           [UserResponseFields.Modules] = branch.Modules,
                           [UserResponseFields.Timestamp] = branch.Timestamp,
                           [UserResponseFields.ActiveWorld] = branch.ActiveWorld,
                           [UserResponseFields.ActiveSim] = branch.ActiveSim
                       });

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.List] = payload
                       });
                   })
           .RequireTokenAuthentication()
           .WithName(BranchesEndpointName);
    }

    private static void MapProtectedNotifyPrefs(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.NotifyPrefs,
                    async ([FromBody] NotifyPreferencesRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserRepository userRepository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        var preferences = CreateNotifyPreferencesDictionary(user.NotifyPrefs);

                        ApplyBooleanPreference(preferences, UserResponseFields.NotifyDisabled, request.Disabled);
                        ApplyBooleanPreference(preferences, UserResponseFields.NotifyDisabledOnMessages, request.DisabledOnMessages);
                        ApplyBooleanPreference(preferences, UserResponseFields.NotifySendOnline, request.SendOnline);
                        ApplyIntervalPreference(preferences, UserResponseFields.NotifyInterval, request.Interval, AllowedNotifyIntervals);
                        ApplyIntervalPreference(preferences, UserResponseFields.NotifyErrorsInterval, request.ErrorsInterval, AllowedNotifyErrorsIntervals);

                        await userRepository.UpdateNotifyPreferencesAsync(user.Id, preferences, cancellationToken).ConfigureAwait(false);

                        return Results.Ok(new Dictionary<string, object?>
                        {
                            [UserResponseFields.NotifyPrefs] = preferences
                        });
                    })
           .RequireTokenAuthentication()
           .WithName(NotifyPrefsEndpointName);
    }

    private static void MapProtectedCode(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Code,
                   async ([FromQuery] string? branch,
                          ICurrentUserAccessor userAccessor,
                          IUserCodeRepository repository,
                          CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       var branchName = branch ?? ActiveWorldIdentifier;
                       var codeBranch = await repository.GetBranchAsync(user.Id, branchName, cancellationToken).ConfigureAwait(false);
                       if (codeBranch is null)
                           return Results.BadRequest(new ErrorResponse(NoCodeMessage));

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Branch] = codeBranch.Branch,
                           [UserResponseFields.Modules] = codeBranch.Modules
                       });
                   })
           .RequireTokenAuthentication()
           .WithName(GetCodeEndpointName);
    }


    private static void MapPublicFind(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Find,
                   async ([FromQuery(Name = UsernameQueryName)] string? username,
                          [FromQuery(Name = UserIdQueryName)] string? userId,
                          IUserRepository userRepository,
                          CancellationToken cancellationToken) =>
                   {
                       if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(userId))
                           return Results.BadRequest(new ErrorResponse(MissingUserIdentifierMessage));

                       var profile = await userRepository.FindPublicProfileAsync(username, userId, cancellationToken).ConfigureAwait(false);
                       if (profile is null)
                           return Results.NotFound(new ErrorResponse(UserNotFoundMessage));

                       var userPayload = new Dictionary<string, object?>
                       {
                           [UserResponseFields.Id] = profile.Id,
                           [UserResponseFields.Username] = profile.Username,
                           [UserResponseFields.Badge] = profile.Badge,
                           [UserResponseFields.Gcl] = profile.Gcl,
                           [UserResponseFields.Power] = profile.Power,
                           [UserResponseFields.Steam] = profile.SteamId is null ? null : new Dictionary<string, object?>
                           {
                               [UserResponseFields.SteamId] = profile.SteamId
                           }
                       };

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.User] = userPayload
                       });
                   })
           .WithName(FindEndpointName);
    }

    private static void MapPublicRooms(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Rooms,
                   async ([FromQuery(Name = UserIdQueryName)] string? userId,
                          IUserWorldRepository userWorldRepository,
                          CancellationToken cancellationToken) =>
                   {
                       if (string.IsNullOrWhiteSpace(userId))
                           return Results.BadRequest(new ErrorResponse(MissingUserIdMessage));

                       var rooms = await userWorldRepository.GetControllerRoomsAsync(userId, cancellationToken).ConfigureAwait(false);
                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Rooms] = rooms
                       });
                   })
           .WithName(RoomsEndpointName);
    }

    private static void MapPublicBadgeSvg(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.BadgeSvg,
                   async ([FromQuery(Name = UsernameQueryName)] string? username,
                          [FromQuery(Name = BorderQueryName)] string? borderValue,
                          IUserRepository userRepository,
                          IBadgeSvgGenerator badgeSvgGenerator,
                          CancellationToken cancellationToken) =>
                   {
                       if (string.IsNullOrWhiteSpace(username))
                           return Results.BadRequest(new ErrorResponse(MissingUsernameMessage));

                       var profile = await userRepository.FindPublicProfileAsync(username, null, cancellationToken).ConfigureAwait(false);
                       if (profile?.Badge is null)
                           return Results.NotFound();

                       var includeBorder = string.Equals(borderValue, BorderEnabledValue, StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(borderValue, BorderEnabledAlternateValue, StringComparison.OrdinalIgnoreCase);
                       var svg = badgeSvgGenerator.GenerateSvg(profile.Badge, includeBorder);
                       return Results.Text(svg, ContentTypes.Svg);
                   })
           .WithName(BadgeSvgEndpointName);
    }

    private static void MapPublicStats(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Stats,
                   ([FromQuery] int? interval) =>
                   {
                       var intervalValue = interval ?? 0;
                       if (!IsValidStatsInterval(intervalValue))
                           return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Stats] = new Dictionary<string, object?>()
                       });
                   })
           .WithName(StatsEndpointName);
    }

    private static IResult NotImplemented(string route)
        => Results.Json(new { error = NotImplementedError, route }, statusCode: StatusCodes.Status501NotImplemented);

    private static UserProfile RequireUser(ICurrentUserAccessor accessor)
        => accessor.CurrentUser ?? throw new InvalidOperationException(MissingUserContextMessage);

    private static bool IsValidStatsInterval(int interval)
        => Array.IndexOf(AllowedStatsIntervals, interval) >= 0;

    private static Dictionary<string, object?> CreateNotifyPreferencesDictionary(object? notifyPrefs)
    {
        switch (notifyPrefs) {
            case IDictionary<string, object?> typedDictionary: return new Dictionary<string, object?>(typedDictionary, StringComparer.Ordinal);
            case IDictionary dictionary: {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key)
                        result[key] = entry.Value;
                }
                return result;
            }
            case JsonElement { ValueKind: JsonValueKind.Object } element: {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                    result[property.Name] = ConvertJsonElementValue(property.Value);
                return result;
            }
            default: return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }

    private static object? ConvertJsonElementValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.String => element.GetString(),
            _ => null
        };

    private static void ApplyBooleanPreference(IDictionary<string, object?> preferences, string key, bool? value)
    {
        if (value.HasValue)
            preferences[key] = value.Value;
    }

    private static void ApplyIntervalPreference(IDictionary<string, object?> preferences, string key, int? value, int[] allowedValues)
    {
        if (value.HasValue && Array.IndexOf(allowedValues, value.Value) >= 0)
            preferences[key] = value.Value;
    }

    private static void MapProtectedRespawnProhibitedRooms(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.RespawnProhibitedRooms,
                   () => Results.Ok(new Dictionary<string, object?>
                   {
                       [UserResponseFields.Rooms] = Array.Empty<string>()
                   }))
           .RequireTokenAuthentication()
           .WithName(RespawnProhibitedRoomsEndpointName);
    }
}
