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
            services.RemoveAll<IServerInfoRepository>();
            services.RemoveAll<IServerInfoProvider>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRoomRepository>();

            services.AddSingleton<IStorageAdapter, FakeStorageAdapter>();
            services.AddSingleton<IServerInfoRepository, FakeServerInfoRepository>();
            services.AddSingleton<IServerInfoProvider, FakeServerInfoProvider>();
            services.AddSingleton<IUserRepository, FakeUserRepository>();
            services.AddSingleton<IRoomRepository, FakeRoomRepository>();
        });
    }
}

sealed file class FakeStorageAdapter : IStorageAdapter
{
    public Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new StorageStatus(true, DateTimeOffset.UtcNow, null));

    public Task<ServerInfo?> GetServerInfoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<ServerInfo?>(new ServerInfo("Test Server", "test-build", true));
}

sealed file class FakeServerInfoRepository : IServerInfoRepository
{
    public ServerInfo GetServerInfo() => new("Test Server", "test-build", true);
}

sealed file class FakeServerInfoProvider : IServerInfoProvider
{
    private readonly ServerInfo _serverInfo = new("Test Server", "test-build", true);

    public ServerInfo GetServerInfo() => _serverInfo;
}

sealed file class FakeUserRepository : IUserRepository
{
    public Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<UserSummary>>(Array.Empty<UserSummary>());
}

sealed file class FakeRoomRepository : IRoomRepository
{
    public Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RoomSummary>>(Array.Empty<RoomSummary>());
}
