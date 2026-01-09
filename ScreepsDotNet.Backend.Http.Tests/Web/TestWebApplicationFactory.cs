using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Storage;
using ScreepsDotNet.Backend.Http.Routing;

namespace ScreepsDotNet.Backend.Http.Tests.Web;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => {
            services.RemoveAll<IStorageAdapter>();
            services.RemoveAll<IVersionInfoProvider>();
            services.RemoveAll<IServerDataRepository>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRoomRepository>();
            services.RemoveAll<IUserWorldRepository>();
            services.RemoveAll<IUserCodeRepository>();
            services.RemoveAll<IUserMemoryRepository>();
            services.RemoveAll<IUserConsoleRepository>();
            services.RemoveAll<IUserMoneyRepository>();
            services.RemoveAll<IMarketOrderRepository>();
            services.RemoveAll<IMarketStatsRepository>();
            services.RemoveAll<IRoomStatusRepository>();
            services.RemoveAll<IRoomTerrainRepository>();
            services.RemoveAll<IWorldStatsRepository>();
            services.RemoveAll<IWorldMetadataRepository>();
            services.RemoveAll<IUserRespawnService>();
            services.RemoveAll<ITokenService>();

            services.AddSingleton<IStorageAdapter, FakeStorageAdapter>();
            services.AddSingleton<IVersionInfoProvider, FakeVersionInfoProvider>();
            services.AddSingleton<IServerDataRepository, FakeServerDataRepository>();
            services.AddSingleton<IUserRepository, FakeUserRepository>();
            services.AddSingleton<IRoomRepository, FakeRoomRepository>();
            services.AddSingleton<IUserWorldRepository, FakeUserWorldRepository>();
            services.AddSingleton<IUserCodeRepository, FakeUserCodeRepository>();
            services.AddSingleton<IUserMemoryRepository, FakeUserMemoryRepository>();
            services.AddSingleton<IUserConsoleRepository, FakeUserConsoleRepository>();
            services.AddSingleton<IUserMoneyRepository, FakeUserMoneyRepository>();
            services.AddSingleton<IMarketOrderRepository, FakeMarketOrderRepository>();
            services.AddSingleton<IMarketStatsRepository, FakeMarketStatsRepository>();
            services.AddSingleton<IRoomStatusRepository, FakeRoomStatusRepository>();
            services.AddSingleton<IRoomTerrainRepository, FakeRoomTerrainRepository>();
            services.AddSingleton<IWorldStatsRepository, FakeWorldStatsRepository>();
            services.AddSingleton<IWorldMetadataRepository, FakeWorldMetadataRepository>();
            services.AddSingleton<IUserRespawnService, FakeUserRespawnService>();
            services.AddSingleton<ITokenService, FakeTokenService>();
            services.Configure<AuthOptions>(options => {
                options.UseNativeAuth = false;
                options.TokenTtlSeconds = 60;
                options.Tickets =
                [
                    new()
                    {
                        Ticket = AuthTestValues.Ticket,
                        UserId = AuthTestValues.UserId,
                        SteamId = AuthTestValues.SteamId
                    }
                ];
            });
        });
    }
}

sealed file class FakeStorageAdapter : IStorageAdapter
{
    public Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new StorageStatus(true, DateTimeOffset.UtcNow, null));
}

internal static class FakeServerDataFactory
{
    public static ServerData Create()
        => new(VersionTestValues.WelcomeText,
               new Dictionary<string, object>(),
               VersionTestValues.HistoryChunkSize,
               VersionTestValues.SocketUpdateThrottle,
               new RendererData(new Dictionary<string, object>(), new Dictionary<string, object>()));
}

sealed file class FakeServerDataRepository : IServerDataRepository
{
    public Task<ServerData> GetServerDataAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(FakeServerDataFactory.Create());
}

sealed file class FakeVersionInfoProvider : IVersionInfoProvider
{
    private readonly VersionInfo _versionInfo;

    public FakeVersionInfoProvider()
    {
        var serverData = FakeServerDataFactory.Create();
        _versionInfo = new VersionInfo(VersionTestValues.Protocol,
                                       VersionTestValues.UseNativeAuth,
                                       VersionTestValues.Users,
                                       serverData,
                                       VersionTestValues.PackageVersion);
    }

    public Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_versionInfo);
}

internal sealed class FakeUserRepository : IUserRepository
{
    public const string DuplicateEmail = "duplicate@example.com";

    private readonly Dictionary<string, object?> _notifyPrefs = new(StringComparer.Ordinal)
    {
        [UserResponseFields.NotifyDisabled] = false,
        [UserResponseFields.NotifyDisabledOnMessages] = false,
        [UserResponseFields.NotifySendOnline] = true,
        [UserResponseFields.NotifyInterval] = 60,
        [UserResponseFields.NotifyErrorsInterval] = 60
    };

    private UserBadgeUpdate? _latestBadge;
    private string _currentEmail = AuthTestValues.Email;
    private bool _steamProfileHidden;

    private static readonly IReadOnlyDictionary<string, object?> PublicBadge = new Dictionary<string, object?>
    {
        [BadgeDocumentFields.Color1] = "#ff0000",
        [BadgeDocumentFields.Color2] = "#00ff00",
        [BadgeDocumentFields.Color3] = "#0000ff",
        [BadgeDocumentFields.Type] = 1,
        [BadgeDocumentFields.Param] = 0
    };

    private static readonly IDictionary<string, object?> CustomBadge = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["src"] = "custom"
    };

    private static readonly UserProfile BaseProfile = new(AuthTestValues.UserId, AuthTestValues.Username, AuthTestValues.Email, false,
                                                      true, 100, null, DateTime.UtcNow.AddDays(-1), null, null, DateTime.UtcNow.AddHours(-2),
                                                      false, CustomBadge, 0, 500, new UserSteamProfile(AuthTestValues.SteamId, "Test Player", null, false),
                                                      0, 0);

    private static readonly UserPublicProfile PublicProfile = new(AuthTestValues.UserId, AuthTestValues.Username, PublicBadge, null, 0, AuthTestValues.SteamId);

    public Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var badge = _latestBadge is null ? null : BuildBadgeDictionary(_latestBadge);
        var steam = BaseProfile.Steam is null
            ? null : BaseProfile.Steam with { SteamProfileLinkHidden = _steamProfileHidden };

        var profile = BaseProfile with
        {
            Email = _currentEmail,
            Badge = badge,
            NotifyPrefs = new Dictionary<string, object?>(_notifyPrefs),
            Steam = steam
        };

        return Task.FromResult<UserProfile?>(profile);
    }

    public Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);

    public Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? userId, CancellationToken cancellationToken = default)
        => Task.FromResult<UserPublicProfile?>(PublicProfile);

    public Task UpdateNotifyPreferencesAsync(string userId, IDictionary<string, object?> notifyPreferences, CancellationToken cancellationToken = default)
    {
        _notifyPrefs.Clear();
        foreach (var kvp in notifyPreferences)
            _notifyPrefs[kvp.Key] = kvp.Value;

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, object?> GetNotifyPreferencesSnapshot() => new Dictionary<string, object?>(_notifyPrefs);

    public UserBadgeUpdate? LastBadgeUpdate => _latestBadge;

    public string CurrentEmail => _currentEmail;

    public bool IsSteamProfileHidden => _steamProfileHidden;

    public Task<bool> UpdateBadgeAsync(string userId, UserBadgeUpdate badge, CancellationToken cancellationToken = default)
    {
        _latestBadge = badge;
        return Task.FromResult(true);
    }

    public Task<EmailUpdateResult> UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        if (string.Equals(email, DuplicateEmail, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(EmailUpdateResult.AlreadyExists);

        _currentEmail = email;
        return Task.FromResult(EmailUpdateResult.Success);
    }

    public Task SetSteamVisibilityAsync(string userId, bool visible, CancellationToken cancellationToken = default)
    {
        _steamProfileHidden = !visible;
        return Task.CompletedTask;
    }

    private static IDictionary<string, object?> BuildBadgeDictionary(UserBadgeUpdate badge)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BadgeDocumentFields.Type] = badge.Type,
            [BadgeDocumentFields.Color1] = badge.Color1,
            [BadgeDocumentFields.Color2] = badge.Color2,
            [BadgeDocumentFields.Color3] = badge.Color3,
            [BadgeDocumentFields.Param] = badge.Param,
            [BadgeDocumentFields.Flip] = badge.Flip
        };
}

sealed file class FakeRoomRepository : IRoomRepository
{
    public Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RoomSummary>>(Array.Empty<RoomSummary>());
}

internal sealed class FakeUserWorldRepository : IUserWorldRepository
{
    public string? ControllerRoom { get; set; } = "W10N10";
    public IReadOnlyCollection<string> ControllerRooms { get; set; } = ["W10N10"];

    public UserWorldStatus WorldStatus { get; set; } = UserWorldStatus.Normal;

    public Task<string?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ControllerRoom);

    public Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(WorldStatus);

    public Task<IReadOnlyCollection<string>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ControllerRooms);
}

internal sealed class FakeUserCodeRepository : IUserCodeRepository
{
    private const string DefaultModuleName = "main";
    private const string DefaultBranchName = "default";
    private const string SimulationBranchName = "sim";
    private const string ActiveWorldBranchIdentifier = "$activeWorld";
    private const string ActiveWorldSlotName = "activeWorld";
    private const string ActiveSimSlotName = "activeSim";
    private const string HelloWorldScript = "console.log('hello');";
    private const string SimulationLoopScript = "module.exports.loop = function() {};";

    private readonly List<UserCodeBranch> _branches =
    [
        new(DefaultBranchName, new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultModuleName] = HelloWorldScript }, DateTime.UtcNow, true, false),
        new(SimulationBranchName, new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultModuleName] = SimulationLoopScript }, DateTime.UtcNow, false, true)
    ];

    public Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<UserCodeBranch>>(_branches.Select(CloneBranch).ToArray());

    public Task<UserCodeBranch?> GetBranchAsync(string userId, string branchIdentifier, CancellationToken cancellationToken = default)
    {
        var branch = ResolveBranch(branchIdentifier);
        return Task.FromResult(branch is null ? null : CloneBranch(branch));
    }

    public Task<bool> UpdateBranchModulesAsync(string userId, string branchIdentifier, IDictionary<string, string> modules, CancellationToken cancellationToken = default)
    {
        var branch = ResolveBranch(branchIdentifier);
        if (branch is null)
            return Task.FromResult(false);

        var updatedBranch = branch with
        {
            Modules = CloneModules(modules),
            Timestamp = DateTime.UtcNow
        };

        ReplaceBranch(branch, updatedBranch);
        return Task.FromResult(true);
    }

    public Task<bool> SetActiveBranchAsync(string userId, string branchName, string activeName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveActiveSlot(activeName, out var slot))
            return Task.FromResult(false);

        var target = FindBranchByName(branchName);
        if (target is null)
            return Task.FromResult(false);

        for (var i = 0; i < _branches.Count; i++) {
            var branch = _branches[i];
            var isTarget = string.Equals(branch.Branch, target.Branch, StringComparison.OrdinalIgnoreCase);
            var activeWorld = slot == ActiveSlot.World ? isTarget : branch.ActiveWorld;
            var activeSim = slot == ActiveSlot.Simulation ? isTarget : branch.ActiveSim;

            if (!isTarget && slot == ActiveSlot.World)
                activeWorld = false;
            if (!isTarget && slot == ActiveSlot.Simulation)
                activeSim = false;

            var updated = branch with
            {
                ActiveWorld = activeWorld,
                ActiveSim = activeSim,
                Timestamp = isTarget ? DateTime.UtcNow : branch.Timestamp
            };

            _branches[i] = updated;
        }

        return Task.FromResult(true);
    }

    public Task<bool> CloneBranchAsync(string userId, string? sourceBranch, string newBranchName, IDictionary<string, string>? defaultModules, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newBranchName) || _branches.Any(b => string.Equals(b.Branch, newBranchName, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(false);

        IReadOnlyDictionary<string, string> modules;
        if (!string.IsNullOrWhiteSpace(sourceBranch)) {
            var source = ResolveBranch(sourceBranch);
            if (source is null)
                return Task.FromResult(false);
            modules = CloneModules(source.Modules);
        }
        else {
            modules = defaultModules is not null && defaultModules.Count > 0
                ? CloneModules(defaultModules)
                : new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultModuleName] = HelloWorldScript };
        }

        _branches.Add(new UserCodeBranch(newBranchName, modules, DateTime.UtcNow, false, false));
        return Task.FromResult(true);
    }

    public Task<bool> DeleteBranchAsync(string userId, string branchName, CancellationToken cancellationToken = default)
    {
        var branch = FindBranchByName(branchName);
        if (branch is null)
            return Task.FromResult(false);

        _branches.RemoveAll(b => string.Equals(b.Branch, branch.Branch, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(true);
    }

    private UserCodeBranch? ResolveBranch(string identifier)
    {
        if (string.Equals(identifier, ActiveWorldBranchIdentifier, StringComparison.OrdinalIgnoreCase))
            return _branches.FirstOrDefault(branch => branch.ActiveWorld) ?? _branches.FirstOrDefault();

        return FindBranchByName(identifier);
    }

    private UserCodeBranch? FindBranchByName(string? name)
        => _branches.FirstOrDefault(branch => string.Equals(branch.Branch, name, StringComparison.OrdinalIgnoreCase));

    private static UserCodeBranch CloneBranch(UserCodeBranch branch)
        => branch with { Modules = CloneModules(branch.Modules) };

    private static IReadOnlyDictionary<string, string> CloneModules(IReadOnlyDictionary<string, string> modules)
        => new Dictionary<string, string>(modules, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> CloneModules(IDictionary<string, string> modules)
        => new Dictionary<string, string>(modules, StringComparer.Ordinal);

    private void ReplaceBranch(UserCodeBranch original, UserCodeBranch replacement)
    {
        var index = _branches.FindIndex(branch => ReferenceEquals(branch, original));
        if (index >= 0)
            _branches[index] = replacement;
    }

    private static bool TryResolveActiveSlot(string activeName, out ActiveSlot slot)
    {
        if (string.Equals(activeName, ActiveWorldSlotName, StringComparison.OrdinalIgnoreCase)) {
            slot = ActiveSlot.World;
            return true;
        }

        if (string.Equals(activeName, ActiveSimSlotName, StringComparison.OrdinalIgnoreCase)) {
            slot = ActiveSlot.Simulation;
            return true;
        }

        slot = default;
        return false;
    }

    private enum ActiveSlot
    {
        World,
        Simulation
    }
}

internal sealed class FakeUserMemoryRepository : IUserMemoryRepository
{
    private readonly Dictionary<string, object?> _memory = new(StringComparer.Ordinal)
    {
        ["rooms"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["W1N1"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["level"] = 1
            }
        }
    };

    private readonly Dictionary<int, string?> _segments = [];

    public Task<IDictionary<string, object?>> GetMemoryAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(CloneDictionary(_memory));

    public Task UpdateMemoryAsync(string userId, string? path, JsonElement value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) {
            if (value.ValueKind == JsonValueKind.Object) {
                _memory.Clear();
                foreach (var property in value.EnumerateObject())
                    _memory[property.Name] = ConvertJson(property.Value);
            }
            else if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                _memory.Clear();
            else
                _memory["value"] = ConvertJson(value);

            return Task.CompletedTask;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return Task.CompletedTask;

        ApplyPathUpdate(_memory, segments, 0, value);
        return Task.CompletedTask;
    }

    public Task<string?> GetMemorySegmentAsync(string userId, int segment, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(_segments.TryGetValue(segment, out var data) ? data ?? string.Empty : string.Empty);

    public Task SetMemorySegmentAsync(string userId, int segment, string? data, CancellationToken cancellationToken = default)
    {
        if (data is null)
            _segments.Remove(segment);
        else
            _segments[segment] = data;

        return Task.CompletedTask;
    }

    private static void ApplyPathUpdate(IDictionary<string, object?> root, IReadOnlyList<string> segments, int index, JsonElement value)
    {
        while (true) {
            var key = segments[index];
            if (index == segments.Count - 1) {
                if (value.ValueKind == JsonValueKind.Undefined)
                    root.Remove(key);
                else
                    root[key] = ConvertJson(value);
                return;
            }

            if (!root.TryGetValue(key, out var child) || child is not IDictionary<string, object?> childDict) {
                childDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                root[key] = childDict;
            }

            root = childDict;
            index += 1;
        }
    }

    private static object? ConvertJson(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => ConvertArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private static IDictionary<string, object?> ConvertObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            dict[property.Name] = ConvertJson(property.Value);
        return dict;
    }

    private static IList<object?> ConvertArray(JsonElement element)
        => element.EnumerateArray().Select(ConvertJson).ToList();

    private static IDictionary<string, object?> CloneDictionary(IDictionary<string, object?> source)
    {
        var clone = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in source)
            clone[kvp.Key] = CloneValue(kvp.Value);
        return clone;
    }

    private static object? CloneValue(object? value)
        => value switch
        {
            IDictionary<string, object?> dict => CloneDictionary(dict),
            IList<object?> list => CloneList(list),
            _ => value
        };

    private static IList<object?> CloneList(IList<object?> source)
    {
        var list = new List<object?>(source.Count);
        list.AddRange(source.Select(CloneValue));
        return list;
    }
}

internal sealed class FakeUserConsoleRepository : IUserConsoleRepository
{
    private readonly List<(string Expression, bool Hidden)> _entries = [];

    public Task EnqueueExpressionAsync(string userId, string expression, bool hidden, CancellationToken cancellationToken = default)
    {
        _entries.Add((expression, hidden));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUserMoneyRepository : IUserMoneyRepository
{
    private const string DateFieldName = "date";
    private const string ChangeFieldName = "change";
    private const string BalanceFieldName = "balance";
    private const string TypeFieldName = "type";
    private const string DescriptionFieldName = "description";
    private const string MarketSellTypeValue = "market.sell";
    private const string SoldEnergyDescription = "Sold energy";

    private readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _entries =
    [
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DateFieldName] = DateTime.UtcNow,
            [ChangeFieldName] = 5000,
            [BalanceFieldName] = 15000,
            [TypeFieldName] = MarketSellTypeValue,
            [DescriptionFieldName] = SoldEnergyDescription
        }
    ];

    public Task<MoneyHistoryPage> GetHistoryAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(new MoneyHistoryPage(page, false, _entries));
}

sealed file class FakeMarketOrderRepository : IMarketOrderRepository
{
    private static readonly IReadOnlyList<MarketOrderSummary> Summaries =
    [
        new("energy", 2, 1, 1)
    ];

    private static readonly IReadOnlyList<MarketOrder> Orders =
    [
        new("order-1", AuthTestValues.UserId, "energy", "sell", "W1N1", 5.0m, 1000, 750, 1000, 1000, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
        new("order-2", null, "energy", "buy", "W2N2", 4.5m, 500, 500, 500, 1001, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    ];

    public Task<IReadOnlyList<MarketOrderSummary>> GetActiveOrderIndexAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Summaries);

    public Task<IReadOnlyList<MarketOrder>> GetActiveOrdersByResourceAsync(string resourceType, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MarketOrder>>(Orders.Where(o => string.Equals(o.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<IReadOnlyList<MarketOrder>> GetOrdersByUserAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MarketOrder>>(Orders.Where(o => string.Equals(o.UserId, userId, StringComparison.Ordinal)).ToList());
}

sealed file class FakeMarketStatsRepository : IMarketStatsRepository
{
    private static readonly IReadOnlyList<MarketStatsEntry> Entries =
    [
        new("energy", "2026-01-06", 10, 5000, 4.8, 0.3)
    ];

    public Task<IReadOnlyList<MarketStatsEntry>> GetStatsAsync(string resourceType, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MarketStatsEntry>>(Entries.Where(e => string.Equals(e.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)).ToList());
}

sealed file class FakeRoomStatusRepository : IRoomStatusRepository
{
    private static readonly RoomStatusInfo Status = new("W1N1", "normal", false, false, null);

    public Task<RoomStatusInfo?> GetRoomStatusAsync(string roomName, string? shardName = null, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Equals(roomName, Status.RoomName, StringComparison.OrdinalIgnoreCase) ? Status : null);

    public Task<IReadOnlyDictionary<string, RoomStatusInfo>> GetRoomStatusesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default)
    {
        var dictionary = rooms.Where(room => string.Equals(room.RoomName, Status.RoomName, StringComparison.OrdinalIgnoreCase))
                              .Select(room => room.RoomName)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToDictionary(name => name, _ => Status, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyDictionary<string, RoomStatusInfo>>(dictionary);
    }
}

sealed file class FakeRoomTerrainRepository : IRoomTerrainRepository
{
    private static readonly IReadOnlyList<RoomTerrainData> Entries =
    [
        new("W1N1", null, "terrain", new string('0', 2500))
    ];

    public Task<IReadOnlyList<RoomTerrainData>> GetTerrainEntriesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default)
    {
        var requested = rooms?.Select(room => room.RoomName)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase)
                       ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = requested.Count == 0
            ? Entries.ToList()
            : Entries.Where(entry => requested.Contains(entry.RoomName)).ToList();
        return Task.FromResult<IReadOnlyList<RoomTerrainData>>(result);
    }
}

sealed file class FakeWorldStatsRepository : IWorldStatsRepository
{
    private const string RoomName = "W1N1";
    private const int GameTime = 12345;

    public Task<MapStatsResult> GetMapStatsAsync(MapStatsRequest request, CancellationToken cancellationToken = default)
    {
        var stats = new Dictionary<string, MapStatsRoom>(StringComparer.OrdinalIgnoreCase)
        {
            [RoomName] = new(RoomName,
                             "normal",
                             false,
                             false,
                             null,
                             new RoomOwnershipInfo(AuthTestValues.UserId, 3),
                             new RoomSignInfo(AuthTestValues.UserId, "hello", 100),
                             true,
                             new RoomMineralInfo("H", 2))
        };

        var users = new Dictionary<string, MapStatsUser>(StringComparer.OrdinalIgnoreCase)
        {
            [AuthTestValues.UserId] = new(AuthTestValues.UserId, AuthTestValues.Username, null)
        };

        return Task.FromResult(new MapStatsResult(GameTime,
                                                  stats,
                                                  new Dictionary<string, object?>(),
                                                  users));
    }
}

sealed file class FakeWorldMetadataRepository : IWorldMetadataRepository
{
    public int Time { get; set; } = 999;

    public int Tick { get; set; } = 500;

    public WorldSize Size { get; set; } = new(10, 10);

    public Task<int> GetGameTimeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Time);

    public Task<int> GetTickDurationAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Tick);

    public Task<WorldSize> GetWorldSizeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Size);
}
internal sealed class FakeUserRespawnService : IUserRespawnService
{
    public UserRespawnResult NextResult { get; set; } = UserRespawnResult.Success;

    public Task<UserRespawnResult> RespawnAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(NextResult);
}

sealed file class FakeTokenService : ITokenService
{
    private const string ValidToken = "test-token";

    public Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ValidToken);

    public Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(token == ValidToken ? AuthTestValues.UserId : null);
}
