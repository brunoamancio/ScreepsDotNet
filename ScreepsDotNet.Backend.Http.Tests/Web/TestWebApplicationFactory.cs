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
            services.RemoveAll<ITokenService>();

            services.AddSingleton<IStorageAdapter, FakeStorageAdapter>();
            services.AddSingleton<IVersionInfoProvider, FakeVersionInfoProvider>();
            services.AddSingleton<IUserRepository, FakeUserRepository>();
            services.AddSingleton<IRoomRepository, FakeRoomRepository>();
            services.AddSingleton<IUserWorldRepository, FakeUserWorldRepository>();
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

sealed file class FakeUserRepository : IUserRepository
{
    private static readonly UserProfile Profile = new(AuthTestValues.UserId, AuthTestValues.Username, AuthTestValues.Email, false,
                                                      true, 100, null, DateTime.UtcNow.AddDays(-1), null, null, DateTime.UtcNow.AddHours(-2),
                                                      false, null, 0, 500, new UserSteamProfile(AuthTestValues.SteamId, "Test Player", null, false),
                                                      0, 0);

    private static readonly UserPublicProfile PublicProfile = new(AuthTestValues.UserId, AuthTestValues.Username, null, null, 0, AuthTestValues.SteamId);

    public Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<UserProfile?>(Profile);

    public Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);

    public Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? userId, CancellationToken cancellationToken = default)
        => Task.FromResult<UserPublicProfile?>(PublicProfile);
}

sealed file class FakeRoomRepository : IRoomRepository
{
    public Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RoomSummary>>(Array.Empty<RoomSummary>());
}

internal sealed class FakeUserWorldRepository : IUserWorldRepository
{
    public string? ControllerRoom { get; set; } = "W10N10";
    public IReadOnlyCollection<string> ControllerRooms { get; set; } = new[] { "W10N10" };

    public UserWorldStatus WorldStatus { get; set; } = UserWorldStatus.Normal;

    public Task<string?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ControllerRoom);

    public Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(WorldStatus);

    public Task<IReadOnlyCollection<string>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ControllerRooms);
}

sealed file class FakeTokenService : ITokenService
{
    private const string ValidToken = "test-token";

    public Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(ValidToken);

    public Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(token == ValidToken ? AuthTestValues.UserId : null);
}
