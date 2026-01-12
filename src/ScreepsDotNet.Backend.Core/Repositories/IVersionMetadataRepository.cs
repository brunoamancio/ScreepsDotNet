namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IVersionMetadataRepository
{
    Task<VersionMetadata> GetAsync(CancellationToken cancellationToken = default);
}
