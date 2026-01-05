using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Storage;

namespace ScreepsDotNet.Backend.Http.Tests.Web;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStorageAdapter>();
            services.RemoveAll<IVersionInfoProvider>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRoomRepository>();
            services.RemoveAll<IUserWorldRepository>();
            services.RemoveAll<IUserCodeRepository>();
            services.RemoveAll<ITokenService>();

            services.AddSingleton<IStorageAdapter, FakeStorageAdapter>();
            services.AddSingleton<IVersionInfoProvider, FakeVersionInfoProvider>();
            services.AddSingleton<IUserRepository, FakeUserRepository>();
            services.AddSingleton<IRoomRepository, FakeRoomRepository>();
            services.AddSingleton<IUserWorldRepository, FakeUserWorldRepository>();
            services.AddSingleton<IUserCodeRepository, FakeUserCodeRepository>();
            services.AddSingleton<ITokenService, FakeTokenService>();
            services.Configure<AuthOptions>(options =>
            {
                options.UseNativeAuth = false;
                options.TokenTtlSeconds = 60;
                options.Tickets = new List<AuthTicketOptions>
                {
                    new()
                    {
                        Ticket = AuthTestValues.Ticket,
                        UserId = AuthTestValues.UserId,
                        SteamId = AuthTestValues.SteamId
                    }
                };
            });
        });
    }
}

sealed file class FakeStorageAdapter : IStorageAdapter
{
    public Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new StorageStatus(true, DateTimeOffset.UtcNow, null));
}

sealed file class FakeVersionInfoProvider : IVersionInfoProvider
{
    private readonly VersionInfo _versionInfo;

    public FakeVersionInfoProvider()
    {
        var serverData = new ServerData(
            VersionTestValues.WelcomeText,
            new Dictionary<string, object>(),
            VersionTestValues.HistoryChunkSize,
            VersionTestValues.SocketUpdateThrottle,
            new RendererData(new Dictionary<string, object>(), new Dictionary<string, object>()));

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
    private readonly Dictionary<string, object?> _notifyPrefs = new(StringComparer.Ordinal)
    {
        ["disabled"] = false,
        ["disabledOnMessages"] = false,
        ["sendOnline"] = true,
        ["interval"] = 60,
        ["errorsInterval"] = 60
    };

    private static readonly IReadOnlyDictionary<string, object?> PublicBadge = new Dictionary<string, object?>
    {
        ["color1"] = "#ff0000",
        ["color2"] = "#00ff00",
        ["color3"] = "#0000ff",
        ["type"] = 1,
        ["param"] = 0
    };

    private static readonly UserProfile BaseProfile = new(AuthTestValues.UserId, AuthTestValues.Username, AuthTestValues.Email, false,
                                                      true, 100, null, DateTime.UtcNow.AddDays(-1), null, null, DateTime.UtcNow.AddHours(-2),
                                                      false, null, 0, 500, new UserSteamProfile(AuthTestValues.SteamId, "Test Player", null, false),
                                                      0, 0);

    private static readonly UserPublicProfile PublicProfile = new(AuthTestValues.UserId, AuthTestValues.Username, PublicBadge, null, 0, AuthTestValues.SteamId);

    public Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<UserProfile?>(BaseProfile with { NotifyPrefs = new Dictionary<string, object?>(_notifyPrefs) });

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
    private const string HelloWorldScript = "console.log('hello');";
    private const string SimulationLoopScript = "module.exports.loop = function() {};";
    private const string ActiveWorldBranchIdentifier = "$activeWorld";

    private readonly List<UserCodeBranch> _branches =
    [
        new(DefaultBranchName, new Dictionary<string, string> { [DefaultModuleName] = HelloWorldScript }, DateTime.UtcNow, true, false),
        new(SimulationBranchName, new Dictionary<string, string> { [DefaultModuleName] = SimulationLoopScript }, DateTime.UtcNow, false, true)
    ];

    public Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<UserCodeBranch>>(_branches);

    public Task<UserCodeBranch?> GetBranchAsync(string userId, string branchIdentifier, CancellationToken cancellationToken = default)
    {
        if (branchIdentifier.Equals(ActiveWorldBranchIdentifier, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<UserCodeBranch?>(_branches.First());

        var branch = _branches.FirstOrDefault(b => string.Equals(b.Branch, branchIdentifier, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(branch);
    }
}

sealed file class FakeTokenService : ITokenService
{
    private const string ValidToken = "test-token";

    public Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ValidToken);

    public Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(token == ValidToken ? AuthTestValues.UserId : null);
}
