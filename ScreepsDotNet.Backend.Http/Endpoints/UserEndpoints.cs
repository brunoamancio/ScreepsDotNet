namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
    private const string InvalidMemorySegmentMessage = "invalid segment ID";
    private const string MemorySizeExceededMessage = "memory size is too large";
    private const string MemoryPathErrorMessage = "Incorrect memory path";
    private const string MemorySegmentLengthExceededMessage = "length limit exceeded";
    private const string ExpressionTooLargeMessage = "expression size is too large";
    private const string MissingModulesMessage = "modules are required.";
    private const string CodePayloadTooLargeMessage = "code length exceeds 5 MB limit";
    private const string BranchOperationFailedMessage = "branch operation failed";

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
    private const string DefaultOverviewStatName = "energyHarvested";
    private static readonly int[] AllowedStatsIntervals = [8, 180, 1440];
    private static readonly int[] AllowedNotifyIntervals = [5, 10, 30, 60, 180, 360, 720, 1440, 4320];
    private static readonly int[] AllowedNotifyErrorsIntervals = [0, 5, 10, 30, 60, 180, 360, 720, 1440, 4320, 100000];
    private const int MaxMemoryBytes = 1024 * 1024;
    private const int MaxConsoleExpressionBytes = 1024;
    private const int MaxMemorySegmentSizeBytes = 100 * 1024;
    private const int MaxCodePayloadBytes = 5 * 1024 * 1024;
    private const int MaxBranchNameLength = 30;

    private const string MemorySettingsKey = "settings";
    private const string MemoryRoomsKey = "rooms";
    private const string MemoryLogLevelKey = "logLevel";
    private const string DefaultMemoryLogLevel = "info";

    private static readonly IReadOnlyDictionary<string, object?> DefaultMemory = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MemorySettingsKey] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MemoryLogLevelKey] = DefaultMemoryLogLevel
        },
        [MemoryRoomsKey] = new Dictionary<string, object?>(StringComparer.Ordinal)
    };

    public static void Map(WebApplication app)
    {
        MapProtectedWorldStartRoom(app);
        MapProtectedWorldStatus(app);
        MapProtectedBranches(app);
        MapProtectedUpdateCode(app);
        MapProtectedCode(app);
        MapProtectedPost(app, ApiRoutes.User.Badge, BadgeEndpointName);
        MapProtectedRespawnProhibitedRooms(app);
        MapProtectedPost(app, ApiRoutes.User.Respawn, RespawnEndpointName);
        MapProtectedSetActiveBranch(app);
        MapProtectedCloneBranch(app);
        MapProtectedDeleteBranch(app);
        MapProtectedMemory(app);
        MapProtectedMemorySegments(app);
        MapProtectedConsole(app);
        MapProtectedNotifyPrefs(app);
        MapProtectedOverview(app);
        MapProtectedTutorialDone(app);
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

    private static void MapProtectedUpdateCode(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.Code,
                    async ([FromBody] UserCodeUpdateRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserCodeRepository repository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        if (request.Modules is null || request.Modules.Count == 0)
                            return Results.BadRequest(new ErrorResponse(MissingModulesMessage));

                        var payloadSize = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(request.Modules));
                        if (payloadSize > MaxCodePayloadBytes)
                            return Results.BadRequest(new ErrorResponse(CodePayloadTooLargeMessage));

                        var branchIdentifier = ResolveBranchIdentifier(request.Branch);
                        if (!IsAllowedBranchIdentifier(branchIdentifier))
                            return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                        var updated = await repository.UpdateBranchModulesAsync(user.Id, branchIdentifier, request.Modules, cancellationToken).ConfigureAwait(false);
                        if (!updated)
                            return Results.BadRequest(new ErrorResponse(BranchOperationFailedMessage));

                        return Results.Ok(CreateTimestampResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(PostCodeEndpointName);
    }

    private static void MapProtectedSetActiveBranch(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.SetActiveBranch,
                    async ([FromBody] SetActiveBranchRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserCodeRepository repository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        if (!IsValidActiveName(request.ActiveName) || !IsValidBranchName(request.Branch))
                            return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                        var success = await repository.SetActiveBranchAsync(user.Id, request.Branch!, request.ActiveName!, cancellationToken).ConfigureAwait(false);
                        if (!success)
                            return Results.BadRequest(new ErrorResponse(BranchOperationFailedMessage));

                        return Results.Ok(CreateTimestampResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(SetActiveBranchEndpointName);
    }

    private static void MapProtectedCloneBranch(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.CloneBranch,
                    async ([FromBody] CloneBranchRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserCodeRepository repository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        if (!IsValidBranchName(request.NewName))
                            return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                        if (!string.IsNullOrWhiteSpace(request.Branch) && !IsValidBranchName(request.Branch))
                            return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                        var success = await repository.CloneBranchAsync(user.Id,
                                                                        string.IsNullOrWhiteSpace(request.Branch) ? null : request.Branch,
                                                                        request.NewName!,
                                                                        request.DefaultModules,
                                                                        cancellationToken).ConfigureAwait(false);
                        if (!success)
                            return Results.BadRequest(new ErrorResponse(BranchOperationFailedMessage));

                        return Results.Ok(CreateTimestampResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(CloneBranchEndpointName);
    }

    private static void MapProtectedDeleteBranch(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.DeleteBranch,
                    async ([FromBody] DeleteBranchRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserCodeRepository repository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        if (!IsValidBranchName(request.Branch))
                            return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                        var success = await repository.DeleteBranchAsync(user.Id, request.Branch!, cancellationToken).ConfigureAwait(false);
                        if (!success)
                            return Results.BadRequest(new ErrorResponse(BranchOperationFailedMessage));

                        return Results.Ok(CreateTimestampResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(DeleteBranchEndpointName);
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

    private static void MapProtectedMemory(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Memory,
                   async ([FromQuery] string? path,
                          ICurrentUserAccessor userAccessor,
                          IUserMemoryRepository memoryRepository,
                          CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       var memory = await memoryRepository.GetMemoryAsync(user.Id, cancellationToken).ConfigureAwait(false);
                       var effectiveMemory = EnsureEffectiveMemory(memory);
                       var value = ResolveMemoryPath(effectiveMemory, path);
                       if (value is null)
                           return Results.Ok(new Dictionary<string, object?> { [UserResponseFields.Data] = MemoryPathErrorMessage });

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Data] = EncodeMemoryValue(value)
                       });
                   })
           .RequireTokenAuthentication()
           .WithName(GetMemoryEndpointName);

        app.MapPost(ApiRoutes.User.Memory,
                    async ([FromBody] MemoryUpdateRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserMemoryRepository memoryRepository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        var json = request.Value.ValueKind == JsonValueKind.Undefined ? "null" : request.Value.GetRawText();
                        if (Encoding.UTF8.GetByteCount(json) > MaxMemoryBytes)
                            return Results.BadRequest(new ErrorResponse(MemorySizeExceededMessage));

                        var path = string.IsNullOrWhiteSpace(request.Path) ? null : request.Path;
                        await memoryRepository.UpdateMemoryAsync(user.Id, path, request.Value, cancellationToken).ConfigureAwait(false);

                        return Results.Ok(CreateTimestampResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(PostMemoryEndpointName);
    }

    private static void MapProtectedMemorySegments(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.MemorySegment,
                   async ([FromQuery(Name = "segment")] int? segment,
                          ICurrentUserAccessor userAccessor,
                          IUserMemoryRepository memoryRepository,
                          CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       if (!IsValidMemorySegment(segment))
                           return Results.BadRequest(new ErrorResponse(InvalidMemorySegmentMessage));

                       var data = await memoryRepository.GetMemorySegmentAsync(user.Id, segment!.Value, cancellationToken).ConfigureAwait(false);
                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Data] = data ?? string.Empty
                       });
                   })
           .RequireTokenAuthentication()
           .WithName(GetMemorySegmentEndpointName);

        app.MapPost(ApiRoutes.User.MemorySegment,
                    async ([FromBody] MemorySegmentRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserMemoryRepository memoryRepository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        if (!IsValidMemorySegment(request.Segment))
                            return Results.BadRequest(new ErrorResponse(InvalidMemorySegmentMessage));

                        var length = request.Data is null ? 0 : Encoding.UTF8.GetByteCount(request.Data);
                        if (length > MaxMemorySegmentSizeBytes)
                            return Results.BadRequest(new ErrorResponse(MemorySegmentLengthExceededMessage));

                        await memoryRepository.SetMemorySegmentAsync(user.Id, request.Segment, request.Data, cancellationToken).ConfigureAwait(false);

                        return Results.Ok(CreateTimestampResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(PostMemorySegmentEndpointName);
    }

    private static void MapProtectedConsole(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.Console,
                    async ([FromBody] ConsoleExpressionRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserConsoleRepository consoleRepository,
                           CancellationToken cancellationToken) =>
                    {
                        var user = RequireUser(userAccessor);
                        var expression = request.Expression ?? string.Empty;
                        var length = Encoding.UTF8.GetByteCount(expression);
                        if (length > MaxConsoleExpressionBytes)
                            return Results.BadRequest(new ErrorResponse(ExpressionTooLargeMessage));

                        await consoleRepository.EnqueueExpressionAsync(user.Id, expression, hidden: false, cancellationToken).ConfigureAwait(false);
                        return Results.Ok(CreateEmptyResponse());
                    })
           .RequireTokenAuthentication()
           .WithName(ConsoleEndpointName);
    }

    private static void MapProtectedOverview(WebApplication app)
    {
        app.MapGet(ApiRoutes.User.Overview,
                   async (ICurrentUserAccessor userAccessor,
                          [FromQuery] int? interval,
                          [FromQuery] string? statName,
                          IUserWorldRepository userWorldRepository,
                          CancellationToken cancellationToken) =>
                   {
                       var user = RequireUser(userAccessor);
                       var intervalValue = interval ?? AllowedStatsIntervals[0];
                       if (!IsValidStatsInterval(intervalValue))
                           return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                       // Accept parameter to keep parity with Node backend even if unused for now.
                       _ = string.IsNullOrWhiteSpace(statName) ? DefaultOverviewStatName : statName;

                       var controllerRooms = await userWorldRepository.GetControllerRoomsAsync(user.Id, cancellationToken).ConfigureAwait(false);
                       var roomsPayload = controllerRooms.Count > 0 ? controllerRooms : [];

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Rooms] = roomsPayload,
                           [UserResponseFields.Stats] = new Dictionary<string, object?>(),
                           [UserResponseFields.StatsMax] = null,
                           [UserResponseFields.Totals] = new Dictionary<string, object?>(),
                           [UserResponseFields.GameTimes] = Array.Empty<object>()
                       });
                   })
           .RequireTokenAuthentication()
           .WithName(OverviewEndpointName);
    }

    private static void MapProtectedTutorialDone(WebApplication app)
    {
        app.MapPost(ApiRoutes.User.TutorialDone,
                    (ICurrentUserAccessor userAccessor) =>
                    {
                        RequireUser(userAccessor);
                        return Results.Ok(new Dictionary<string, object?>());
                    })
           .RequireTokenAuthentication()
           .WithName(TutorialDoneEndpointName);
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
                   async ([FromQuery] int? interval,
                          IUserRepository userRepository,
                          IRoomRepository roomRepository,
                          CancellationToken cancellationToken) =>
                   {
                       var intervalValue = interval ?? AllowedStatsIntervals[0];
                       if (!IsValidStatsInterval(intervalValue))
                           return Results.BadRequest(new ErrorResponse(InvalidStatsIntervalMessage));

                       var activeUsers = await userRepository.GetActiveUsersCountAsync(cancellationToken).ConfigureAwait(false);
                       var rooms = await roomRepository.GetOwnedRoomsAsync(cancellationToken).ConfigureAwait(false);

                       var statsPayload = new Dictionary<string, object?>
                       {
                           [UserResponseFields.Interval] = intervalValue,
                           [UserResponseFields.ActiveUsers] = activeUsers,
                           [UserResponseFields.RoomsControlled] = rooms.Count
                       };

                       return Results.Ok(new Dictionary<string, object?>
                       {
                           [UserResponseFields.Stats] = statsPayload
                       });
                   })
           .WithName(StatsEndpointName);
   }

    private static UserProfile RequireUser(ICurrentUserAccessor accessor)
        => accessor.CurrentUser ?? throw new InvalidOperationException(MissingUserContextMessage);

    private static bool IsValidStatsInterval(int interval)
        => Array.IndexOf(AllowedStatsIntervals, interval) >= 0;

    private static bool IsValidMemorySegment(int? segmentId)
        => segmentId is >= 0 and <= 99;

    private static bool IsValidBranchName(string? branchName)
        => !string.IsNullOrWhiteSpace(branchName) && branchName.Length <= MaxBranchNameLength;

    private static bool IsValidActiveName(string? activeName)
        => string.Equals(activeName, UserResponseFields.ActiveWorld, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(activeName, UserResponseFields.ActiveSim, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, object?> EnsureEffectiveMemory(IDictionary<string, object?> memory)
        => memory.Count == 0
            ? DefaultMemory
            : memory as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(memory, StringComparer.Ordinal);

    private static string ResolveBranchIdentifier(string? branch)
        => string.IsNullOrWhiteSpace(branch) ? ActiveWorldIdentifier : branch.Trim();

    private static bool IsAllowedBranchIdentifier(string branchIdentifier)
        => string.Equals(branchIdentifier, ActiveWorldIdentifier, StringComparison.OrdinalIgnoreCase) ||
           IsValidBranchName(branchIdentifier);

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

    private static string EncodeMemoryValue(object value)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            var json = JsonSerializer.Serialize(value);
            writer.Write(json);
        }

        return MemoryConstants.GzipPrefix + Convert.ToBase64String(buffer.ToArray());
    }

    private static object? ResolveMemoryPath(IReadOnlyDictionary<string, object?> root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return root;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = root;
        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> dictionary)
            {
                if (!dictionary.TryGetValue(segment, out current))
                    return null;

                continue;
            }

            if (current is IDictionary<string, object?> mutableDictionary)
            {
                if (!mutableDictionary.TryGetValue(segment, out current))
                    return null;

                continue;
            }

            return null;
        }

        return current;
    }

    private static Dictionary<string, object?> CreateEmptyResponse()
        => new(StringComparer.Ordinal);

    private static Dictionary<string, object?> CreateTimestampResponse()
        => new(StringComparer.Ordinal)
        {
            [UserResponseFields.Timestamp] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    private static IResult NotImplemented(string route)
        => Results.Json(new { error = NotImplementedError, route }, statusCode: StatusCodes.Status501NotImplemented);

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
