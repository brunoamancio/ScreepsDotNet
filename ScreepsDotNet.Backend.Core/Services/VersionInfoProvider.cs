using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;

namespace ScreepsDotNet.Backend.Core.Services;

public sealed class VersionInfoProvider(IUserRepository userRepository, IServerDataRepository serverDataRepository, IOptions<VersionInfoOptions> versionOptions)
    : IVersionInfoProvider
{
    public async Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetActiveUsersCountAsync(cancellationToken).ConfigureAwait(false);
        var serverData = await serverDataRepository.GetServerDataAsync(cancellationToken).ConfigureAwait(false);
        var version = versionOptions.Value;

        return new VersionInfo(version.ProtocolVersion, version.UseNativeAuth, users, serverData, version.PackageVersion);
    }
}
