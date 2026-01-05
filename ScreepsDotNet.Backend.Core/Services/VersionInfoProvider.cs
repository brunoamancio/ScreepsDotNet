using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;

namespace ScreepsDotNet.Backend.Core.Services;

public sealed class VersionInfoProvider : IVersionInfoProvider
{
    private readonly IUserRepository _userRepository;
    private readonly IOptionsMonitor<ServerDataOptions> _serverDataOptions;
    private readonly IOptions<VersionInfoOptions> _versionOptions;

    public VersionInfoProvider(IUserRepository userRepository, IOptionsMonitor<ServerDataOptions> serverDataOptions, IOptions<VersionInfoOptions> versionOptions)
    {
        _userRepository = userRepository;
        _serverDataOptions = serverDataOptions;
        _versionOptions = versionOptions;
    }

    public async Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetActiveUsersCountAsync(cancellationToken).ConfigureAwait(false);
        var serverData = BuildServerData(_serverDataOptions.CurrentValue);
        var version = _versionOptions.Value;

        return new VersionInfo(version.ProtocolVersion, version.UseNativeAuth, users, serverData, version.PackageVersion);
    }

    private static ServerData BuildServerData(ServerDataOptions options)
    {
        return new ServerData(options.WelcomeText, new Dictionary<string, object>(options.CustomObjectTypes), options.HistoryChunkSize,
                              options.SocketUpdateThrottle, new RendererData(new Dictionary<string, object>(options.Renderer.Resources),
                                                                             new Dictionary<string, object>(options.Renderer.Metadata)));
    }
}
