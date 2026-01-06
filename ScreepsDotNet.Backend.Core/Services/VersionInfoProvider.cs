using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;

namespace ScreepsDotNet.Backend.Core.Services;

public sealed class VersionInfoProvider : IVersionInfoProvider
{
    private readonly IUserRepository _userRepository;
    private readonly IServerDataRepository _serverDataRepository;
    private readonly IOptions<VersionInfoOptions> _versionOptions;

    public VersionInfoProvider(IUserRepository userRepository, IServerDataRepository serverDataRepository, IOptions<VersionInfoOptions> versionOptions)
    {
        _userRepository = userRepository;
        _serverDataRepository = serverDataRepository;
        _versionOptions = versionOptions;
    }

    public async Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetActiveUsersCountAsync(cancellationToken).ConfigureAwait(false);
        var serverData = await _serverDataRepository.GetServerDataAsync(cancellationToken).ConfigureAwait(false);
        var version = _versionOptions.Value;

        return new VersionInfo(version.ProtocolVersion, version.UseNativeAuth, users, serverData, version.PackageVersion);
    }
}
