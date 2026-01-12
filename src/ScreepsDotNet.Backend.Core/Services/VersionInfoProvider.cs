using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;

namespace ScreepsDotNet.Backend.Core.Services;

public sealed class VersionInfoProvider(IUserRepository userRepository,
                                        IServerDataRepository serverDataRepository,
                                        IVersionMetadataRepository versionMetadataRepository,
                                        IModManifestProvider modManifestProvider)
    : IVersionInfoProvider
{
    public async Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await versionMetadataRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var users = await userRepository.GetActiveUsersCountAsync(cancellationToken).ConfigureAwait(false);
        var serverData = await serverDataRepository.GetServerDataAsync(cancellationToken).ConfigureAwait(false);
        var manifest = await modManifestProvider.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        var mergedServerData = serverData.WithCustomObjectOverrides(manifest.CustomObjectTypes);

        return new VersionInfo(metadata.ProtocolVersion, metadata.UseNativeAuth, users, mergedServerData, metadata.PackageVersion);
    }
}
