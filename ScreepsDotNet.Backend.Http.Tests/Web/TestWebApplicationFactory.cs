using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

            services.AddSingleton<IStorageAdapter, FakeStorageAdapter>();
            services.AddSingleton<IVersionInfoProvider, FakeVersionInfoProvider>();
            services.AddSingleton<IUserRepository, FakeUserRepository>();
            services.AddSingleton<IRoomRepository, FakeRoomRepository>();
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
    public Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);
}

sealed file class FakeRoomRepository : IRoomRepository
{
    public Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RoomSummary>>(Array.Empty<RoomSummary>());
}
